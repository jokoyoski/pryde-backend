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
            ReferenceId = null,
            ProviderReference = kyc.ProviderReference!,
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

        var correlation = await CorrelateWebhookAsync(
            webhook,
            cancellationToken);
        var kyc = correlation.Kyc;
        var dojahReferenceChanged = false;

        if (!string.IsNullOrWhiteSpace(correlation.DojahReference))
        {
            var referenceOwner = await unitOfWork.KycVerifications
                .GetByDojahReferenceAsync(
                    correlation.DojahReference,
                    cancellationToken);
            if (referenceOwner is not null && referenceOwner.Id != kyc.Id)
            {
                logger.LogWarning(
                    "Dojah webhook reference mismatch rejected for KYC {KycId}.",
                    kyc.Id);
                throw new ValidationException(
                    "Dojah webhook reference belongs to another verification.");
            }

            if (!string.IsNullOrWhiteSpace(kyc.DojahReference) &&
                !kyc.DojahReference.Equals(
                    correlation.DojahReference,
                    StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Dojah webhook reference mismatch rejected for KYC {KycId}.",
                    kyc.Id);
                throw new ValidationException(
                    "Dojah webhook reference does not match the existing verification.");
            }

            if (string.IsNullOrWhiteSpace(kyc.DojahReference))
            {
                kyc.DojahReference = correlation.DojahReference;
                dojahReferenceChanged = true;
            }
        }

        if (kyc.Status == KycStatus.Approved &&
            !webhook.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            if (dojahReferenceChanged)
            {
                unitOfWork.KycVerifications.Update(kyc);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Dojah reference saved for KYC {KycId}.",
                    kyc.Id);
            }

            logger.LogInformation(
                "Dojah webhook ignored because KYC {KycId} is already approved.",
                kyc.Id);
            return;
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
            logger.LogInformation(
                "Duplicate Dojah webhook ignored for KYC {KycId}.",
                kyc.Id);
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

        if (dojahReferenceChanged)
        {
            logger.LogInformation(
                "Dojah reference saved for KYC {KycId}.",
                kyc.Id);
        }

        if (correlation.IsLegacy)
        {
            logger.LogInformation(
                "Legacy Pryde-reference-only Dojah webhook processed for KYC {KycId}.",
                kyc.Id);
        }
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

        var referenceId = GetRequiredString(root, "reference_id");
        var status = GetRequiredString(root, "verification_status");
        var message = root.TryGetProperty("message", out var messageElement) &&
                      messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : null;

        var providerReference = GetMetadataReference(root);

        return new DojahWebhookData(
            referenceId,
            providerReference,
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

        return GetOptionalString(metadata, "kyc_reference");
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
        var queryParts = new List<string>();
        foreach (var pair in uriBuilder.Query
                     .TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            if (key.Equals("reference_id", StringComparison.OrdinalIgnoreCase) ||
                key.Equals(
                    "metadata[kyc_reference]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            queryParts.Add(pair);
        }

        queryParts.Add(
            $"{Uri.EscapeDataString("metadata[kyc_reference]")}=" +
            $"{Uri.EscapeDataString(customReference)}");
        uriBuilder.Query = string.Join('&', queryParts);

        return uriBuilder.Uri.AbsoluteUri;
    }

    private async Task<WebhookCorrelation> CorrelateWebhookAsync(
        DojahWebhookData webhook,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(webhook.ProviderReference))
        {
            var kyc = await unitOfWork.KycVerifications
                .GetByProviderReferenceAsync(
                    webhook.ProviderReference,
                    cancellationToken);
            if (kyc is null)
            {
                var existingByDojah = IsPrydeOwnedReference(webhook.ReferenceId)
                    ? null
                    : await unitOfWork.KycVerifications
                        .GetByDojahReferenceAsync(
                            webhook.ReferenceId,
                            cancellationToken);
                if (existingByDojah is not null)
                {
                    logger.LogWarning(
                        "Dojah webhook ProviderReference mismatch rejected for KYC {KycId}.",
                        existingByDojah.Id);
                    throw new ValidationException(
                        "Dojah webhook correlation reference does not match the verification.");
                }

                throw new NotFoundException(
                    nameof(KycVerification),
                    webhook.ProviderReference);
            }

            logger.LogInformation(
                "Dojah webhook correlated by ProviderReference for KYC {KycId}.",
                kyc.Id);

            if (IsPrydeOwnedReference(webhook.ReferenceId))
            {
                if (!webhook.ReferenceId.Equals(
                        webhook.ProviderReference,
                        StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Dojah webhook ProviderReference mismatch rejected for KYC {KycId}.",
                        kyc.Id);
                    throw new ValidationException(
                        "Dojah webhook correlation references do not match.");
                }

                return new WebhookCorrelation(kyc, null, true);
            }

            return new WebhookCorrelation(
                kyc,
                webhook.ReferenceId,
                false);
        }

        var legacyKyc = await unitOfWork.KycVerifications
            .GetByProviderReferenceAsync(
                webhook.ReferenceId,
                cancellationToken);
        if (legacyKyc is not null)
        {
            logger.LogInformation(
                "Dojah webhook correlated by ProviderReference for KYC {KycId}.",
                legacyKyc.Id);
            return new WebhookCorrelation(legacyKyc, null, true);
        }

        if (IsPrydeOwnedReference(webhook.ReferenceId))
        {
            throw new NotFoundException(
                nameof(KycVerification),
                webhook.ReferenceId);
        }

        var kycByDojahReference = await unitOfWork.KycVerifications
            .GetByDojahReferenceAsync(
                webhook.ReferenceId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(KycVerification),
                webhook.ReferenceId);
        return new WebhookCorrelation(
            kycByDojahReference,
            webhook.ReferenceId,
            false);
    }

    private static bool IsPrydeOwnedReference(string reference) =>
        reference.StartsWith("PRYDE-", StringComparison.OrdinalIgnoreCase);

    private sealed record DojahWebhookData(
        string ReferenceId,
        string? ProviderReference,
        string Status,
        string? Message);

    private sealed record WebhookCorrelation(
        KycVerification Kyc,
        string? DojahReference,
        bool IsLegacy);
}
