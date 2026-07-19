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
            ShareableLink = _settings.ShareableLink,
            WidgetId = widgetId,
            ReferenceId = kyc.ProviderReference!,
            Status = kyc.Status
        };
    }

    public async Task ProcessWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSignature(payload.Span, signature);

        DojahWebhookData webhook;
        try
        {
            webhook = ParseWebhook(payload.Span);
        }
        catch (JsonException)
        {
            throw new ValidationException("Invalid Dojah webhook payload.");
        }

        var kyc = await unitOfWork.KycVerifications.GetByProviderReferenceAsync(
            webhook.ReferenceId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), webhook.ReferenceId);

        if (kyc.Status == KycStatus.Approved &&
            !webhook.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nextStatus = MapStatus(webhook.Status, kyc.Status);
        var rejectionReason = nextStatus == KycStatus.Rejected
            ? SanitizeReason(webhook.Message)
            : null;

        if (kyc.ProviderStatus == webhook.Status &&
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

    private void ValidateSignature(ReadOnlySpan<byte> payload, string? signature)
    {
        var privateKey = _settings.PrivateKey ?? string.Empty;
        logger.LogInformation(
            "Dojah webhook signature diagnostic: SignatureHeaderPresent={SignatureHeaderPresent}, SignatureLength={SignatureLength}, PrivateKeyPresent={PrivateKeyPresent}, PrivateKeyLength={PrivateKeyLength}, PayloadLength={PayloadLength}.",
            !string.IsNullOrWhiteSpace(signature),
            signature?.Length ?? 0,
            !string.IsNullOrWhiteSpace(privateKey),
            privateKey.Length,
            payload.Length);

        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new UnauthorizedException("Invalid webhook signature.");
        }

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

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(privateKey));
        var expected = Convert.ToHexString(hmac.ComputeHash(payload.ToArray()))
            .ToLowerInvariant();
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

        return new DojahWebhookData(referenceId, status.Trim(), message);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new JsonException($"Missing {propertyName}.");
        }

        return element.GetString()!.Trim();
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

    private sealed record DojahWebhookData(
        string ReferenceId,
        string Status,
        string? Message);
}
