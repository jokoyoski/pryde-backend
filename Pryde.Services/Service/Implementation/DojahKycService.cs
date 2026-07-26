using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public class DojahKycService(
    IUnitOfWork unitOfWork,
    IOptions<DojahSettings> options,
    ILogger<DojahKycService> logger) : IDojahKycService
{
    private const string ProviderName = "Dojah";
    private readonly DojahSettings _settings = options.Value;

    public async Task<DojahKycConfigResponseDto> GetConfigAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(userId, cancellationToken);
        var requiresSave = false;

        if (kyc is null)
        {
            kyc = new KycVerification
            {
                UserId = userId,
                Status = KycStatus.Pending,
                ProviderName = ProviderName,
                ProviderReference = CreateReference()
            };
            await unitOfWork.KycVerifications.CreateAsync(kyc, cancellationToken);
            requiresSave = true;
        }
        else if (string.IsNullOrWhiteSpace(kyc.ProviderReference))
        {
            kyc.ProviderName = ProviderName;
            kyc.ProviderReference = CreateReference();
            unitOfWork.KycVerifications.Update(kyc);
            requiresSave = true;
        }

        if (requiresSave)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var shareableUri = new Uri(_settings.ShareableLink, UriKind.Absolute);
        var widgetId = DojahSettingsValidator.GetWidgetId(shareableUri)
            ?? throw new ServiceUnavailableException("Dojah widget configuration is unavailable.");

        return new DojahKycConfigResponseDto
        {
            AppId = _settings.AppId,
            PublicKey = _settings.PublicKey,
            ShareableLink = CreateCorrelatedShareableLink(
                _settings.ShareableLink,
                kyc.ProviderReference!),
            WidgetId = widgetId,
            ReferenceId = kyc.ProviderReference!,
            Metadata = new Dictionary<string, string>
            {
                ["kyc_reference"] = kyc.ProviderReference!
            },
            Status = kyc.Status
        };
    }

    public async Task ProcessWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signatureV1,
        string? signatureV2,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSignature(payload.Span, signatureV1, signatureV2);

        DojahWebhookData webhook;
        try
        {
            webhook = ParseWebhook(payload.Span);
        }
        catch (JsonException)
        {
            throw new ValidationException("Invalid Dojah webhook payload.");
        }

        var customReference = webhook.CustomReference;
        if (string.IsNullOrWhiteSpace(customReference) &&
            webhook.DojahReference.StartsWith("PRYDE-", StringComparison.OrdinalIgnoreCase))
        {
            customReference = webhook.DojahReference;
        }

        KycVerification? kyc;
        if (!string.IsNullOrWhiteSpace(customReference))
        {
            kyc = await unitOfWork.KycVerifications.GetByProviderReferenceAsync(
                customReference,
                cancellationToken);
        }
        else
        {
            kyc = await unitOfWork.KycVerifications.GetByDojahReferenceAsync(
                webhook.DojahReference,
                cancellationToken);
        }

        kyc = kyc ?? throw new NotFoundException(
            nameof(KycVerification),
            customReference ?? webhook.DojahReference);

        if (kyc.Status == KycStatus.Approved &&
            !webhook.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dojahReferenceChanged = false;
        if (webhook.DojahReference.StartsWith("DJ-", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(kyc.DojahReference) &&
                !kyc.DojahReference.Equals(
                    webhook.DojahReference,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "Dojah webhook reference does not match the existing verification.");
            }

            if (string.IsNullOrWhiteSpace(kyc.DojahReference))
            {
                kyc.DojahReference = webhook.DojahReference;
                dojahReferenceChanged = true;
            }
        }

        var nextStatus = MapStatus(webhook.Status, kyc.Status);
        var rejectionReason = nextStatus == KycStatus.Rejected
            ? SanitizeReason(webhook.Message)
            : null;

        if (!dojahReferenceChanged &&
            kyc.ProviderStatus == webhook.Status &&
            kyc.Status == nextStatus &&
            kyc.RejectionReason == rejectionReason)
        {
            return;
        }

        kyc.ProviderName = ProviderName;
        kyc.ProviderStatus = webhook.Status;
        kyc.LastProviderUpdatedAt = DateTime.UtcNow;
        kyc.Status = nextStatus;
        kyc.RejectionReason = rejectionReason;
        kyc.VerifiedAt = nextStatus == KycStatus.Approved ? DateTime.UtcNow : null;

        unitOfWork.KycVerifications.Update(kyc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled)
        {
            throw new ServiceUnavailableException(
                "Dojah verification is currently unavailable. Manual KYC remains available.");
        }
    }

    private void ValidateSignature(
        ReadOnlySpan<byte> payload,
        string? signatureV1,
        string? signatureV2)
    {
        var privateKey = _settings.PrivateKey ?? string.Empty;
        logger.LogInformation(
            "Dojah webhook signature diagnostic: SignatureV1Present={SignatureV1Present}, SignatureV1Length={SignatureV1Length}, SignatureV2Present={SignatureV2Present}, SignatureV2Length={SignatureV2Length}, PrivateKeyPresent={PrivateKeyPresent}, PrivateKeyLength={PrivateKeyLength}, PayloadLength={PayloadLength}.",
            !string.IsNullOrWhiteSpace(signatureV1),
            signatureV1?.Length ?? 0,
            !string.IsNullOrWhiteSpace(signatureV2),
            signatureV2?.Length ?? 0,
            !string.IsNullOrWhiteSpace(privateKey),
            privateKey.Length,
            payload.Length);

        if (!string.IsNullOrWhiteSpace(signatureV2))
        {
            var expectedV2 = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(privateKey)))
                .ToLowerInvariant();
            ValidateHexSignature(signatureV2, expectedV2);
            return;
        }

        if (!string.IsNullOrWhiteSpace(signatureV1))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(privateKey));
            var expectedV1 = Convert.ToHexString(hmac.ComputeHash(payload.ToArray()))
                .ToLowerInvariant();
            ValidateHexSignature(signatureV1, expectedV1);
            return;
        }

        throw new UnauthorizedException("Invalid webhook signature.");
    }

    private static void ValidateHexSignature(string signature, string expected)
    {
        var supplied = signature.Trim();
        const string signaturePrefix = "sha256=";
        if (supplied.StartsWith(signaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            supplied = supplied[signaturePrefix.Length..].Trim();
        }

        if (supplied.Length != 64 || !supplied.All(Uri.IsHexDigit))
        {
            throw new UnauthorizedException("Invalid webhook signature.");
        }

        var suppliedBytes = Encoding.ASCII.GetBytes(supplied.ToLowerInvariant());
        var expectedBytes = Encoding.ASCII.GetBytes(expected);

        if (!CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
        {
            throw new UnauthorizedException("Invalid webhook signature.");
        }
    }

    private static DojahWebhookData ParseWebhook(ReadOnlySpan<byte> payload)
    {
        using var document = JsonDocument.Parse(payload.ToArray());
        var root = document.RootElement;

        var dojahReference = GetRequiredString(root, "reference_id");
        var status = GetRequiredString(root, "verification_status");
        var message = root.TryGetProperty("message", out var messageElement) &&
                      messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : null;

        var customReference =
            GetOptionalString(root, "vendor_reference") ??
            GetOptionalString(root, "customer_reference") ??
            GetOptionalString(root, "custom_reference") ??
            GetMetadataReference(root);

        return new DojahWebhookData(
            dojahReference,
            customReference,
            status.Trim(),
            message);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new JsonException($"Missing {propertyName}.");
        }

        return ValidateReferenceLength(element.GetString()!.Trim(), propertyName);
    }

    private static string? GetOptionalString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            return null;
        }

        return ValidateReferenceLength(property.GetString()!.Trim(), propertyName);
    }

    private static string? GetMetadataReference(JsonElement root)
    {
        if (!root.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetOptionalString(metadata, "kyc_reference") ??
               GetOptionalString(metadata, "vendor_reference") ??
               GetOptionalString(metadata, "customer_reference") ??
               GetOptionalString(metadata, "custom_reference") ??
               GetOptionalString(metadata, "reference_id") ??
               GetOptionalString(metadata, "user_id");
    }

    private static string ValidateReferenceLength(
        string value,
        string propertyName)
    {
        if (value.Length > 100)
        {
            throw new JsonException($"{propertyName} is too long.");
        }

        return value;
    }

    private static KycStatus MapStatus(string providerStatus, KycStatus currentStatus)
    {
        if (providerStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return KycStatus.Approved;
        }

        if (providerStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return KycStatus.Rejected;
        }

        return currentStatus == KycStatus.Approved
            ? KycStatus.Approved
            : KycStatus.Pending;
    }

    private static string SanitizeReason(string? reason)
    {
        const string fallback = "Dojah verification checks were not completed successfully.";
        if (string.IsNullOrWhiteSpace(reason))
        {
            return fallback;
        }

        var sanitized = string.Join(' ', reason.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }

    private static string CreateReference() => $"PRYDE-{Guid.NewGuid():N}";

    private static string CreateCorrelatedShareableLink(
        string shareableLink,
        string customReference)
    {
        var uriBuilder = new UriBuilder(shareableLink);
        var existingQuery = uriBuilder.Query.TrimStart('?');
        var encodedReference = Uri.EscapeDataString(customReference);
        var correlationQuery =
            $"reference_id={encodedReference}&metadata%5Bkyc_reference%5D={encodedReference}";

        uriBuilder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? correlationQuery
            : $"{existingQuery}&{correlationQuery}";

        return uriBuilder.Uri.AbsoluteUri;
    }

    private sealed record DojahWebhookData(
        string DojahReference,
        string? CustomReference,
        string Status,
        string? Message);
}
