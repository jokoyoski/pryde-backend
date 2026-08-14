using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Services.Providers.Kyc;

namespace Pryde.Services.Service.Implementation;

public class DojahKycProvider(
    IUnitOfWork unitOfWork,
    IOptions<DojahSettings> options,
    ILogger<DojahKycProvider> logger,
    INotificationService notificationService,
    IOptions<KycSettings>? kycOptions = null) : IDojahKycService, IKycProvider
{
    private const string ProviderName = "Dojah";
    private static readonly JsonSerializerOptions WebhookSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => ProviderName;

    private readonly DojahSettings _settings = options.Value;
    private readonly KycSettings? _kycSettings = kycOptions?.Value;

    public DojahKycProvider(
        IUnitOfWork unitOfWork,
        IOptions<DojahSettings> options,
        ILogger<DojahKycProvider> logger)
        : this(
            unitOfWork,
            options,
            logger,
            new NotificationService(unitOfWork))
    {
    }

    public async Task<KycProviderResult> CreateSessionAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken = default) =>
        ToProviderResult(await GetConfigAsync(request.UserId, cancellationToken));

    async Task<KycProviderResult> IKycProvider.RetryAsync(
        KycProviderRequest request,
        CancellationToken cancellationToken) =>
        ToProviderResult(await RetryAsync(request.UserId, cancellationToken));

    public async Task<DojahKycConfigResponseDto> GetConfigAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var kyc = await unitOfWork.KycVerifications
            .GetByUserIdAsync(userId, cancellationToken);

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

            await unitOfWork.KycVerifications.CreateAsync(
                kyc,
                cancellationToken);

            requiresSave = true;
        }
        else if (!string.IsNullOrWhiteSpace(kyc.ProviderName) &&
                 !string.Equals(
                     kyc.ProviderName,
                     ProviderName,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The pending KYC verification belongs to another provider.");
        }
        else if (kyc.ProviderReference?.StartsWith(
                     "SMILE-GROUP-",
                     StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ConflictException(
                "The pending KYC verification belongs to another provider.");
        }
        else if (string.IsNullOrWhiteSpace(kyc.ProviderReference))
        {
            kyc.ProviderName = ProviderName;
            kyc.ProviderReference = CreateReference();

            unitOfWork.KycVerifications.Update(kyc);

            requiresSave = true;
        }
        else if (string.IsNullOrWhiteSpace(kyc.ProviderName))
        {
            kyc.ProviderName = ProviderName;
            unitOfWork.KycVerifications.Update(kyc);
            requiresSave = true;
        }

        if (await EnsureAttemptExistsAsync(kyc, cancellationToken))
        {
            requiresSave = true;
        }

        if (requiresSave)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return CreateConfigResponse(kyc);
    }

    public async Task<DojahKycConfigResponseDto> RetryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var kyc = await unitOfWork.KycVerifications
                    .GetByUserIdForUpdateAsync(
                        userId,
                        transactionToken)
                    ?? throw new NotFoundException(
                        nameof(KycVerification),
                        userId);

                if (kyc.Status != KycStatus.Rejected)
                {
                    throw new ConflictException(
                        "Only rejected KYC verification can be retried.");
                }

                if (string.Equals(
                        kyc.ProviderName,
                        ProviderName,
                        StringComparison.OrdinalIgnoreCase) &&
                    kyc.ProviderReference?.StartsWith(
                        "SMILE-GROUP-",
                        StringComparison.OrdinalIgnoreCase) != true)
                {
                    await EnsureAttemptExistsAsync(kyc, transactionToken);
                }

                kyc.Status = KycStatus.Pending;
                kyc.VerifiedAt = null;
                kyc.ProviderName = ProviderName;
                kyc.ProviderReference = CreateReference();
                kyc.DojahReference = null;
                kyc.ProviderStatus = null;
                kyc.RejectionReason = null;
                kyc.LastProviderUpdatedAt = null;

                await EnsureAttemptExistsAsync(
                    kyc,
                    transactionToken,
                    DateTime.UtcNow);

                unitOfWork.KycVerifications.Update(kyc);

                await unitOfWork.SaveChangesAsync(transactionToken);

                return CreateConfigResponse(kyc);
            },
            cancellationToken);
    }

    public async Task ProcessWebhookAsync(
        ReadOnlyMemory<byte> payload,
        string? signatureV1,
        string? signatureV2,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        ValidateSignature(
            payload.Span,
            signatureV1,
            signatureV2);

        DojahWebhookData webhook;

        try
        {
            webhook = ParseWebhook(payload.Span);
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "Invalid Dojah webhook payload.");
        }

        await unitOfWork.ExecuteInTransactionOnceAsync(
            async transactionToken =>
            {
                await ProcessValidatedWebhookAsync(
                    webhook,
                    transactionToken);

                return true;
            },
            cancellationToken);
    }

    private DojahKycConfigResponseDto CreateConfigResponse(
        KycVerification kyc)
    {
        var shareableUri = new Uri(
            _settings.ShareableLink,
            UriKind.Absolute);

        var widgetId = DojahSettingsValidator.GetWidgetId(shareableUri)
            ?? throw new ServiceUnavailableException(
                "Dojah widget configuration is unavailable.");

        return new DojahKycConfigResponseDto
        {
            AppId = _settings.AppId,
            PublicKey = _settings.PublicKey,
            ShareableLink = CreateCorrelatedShareableLink(
                _settings.ShareableLink,
                kyc.ProviderReference!),
            WidgetId = widgetId,
            ReferenceId = kyc.ProviderReference!,
            ProviderReference = kyc.ProviderReference!,
            Metadata = new Dictionary<string, string>
            {
                ["kyc_reference"] = kyc.ProviderReference!
            },
            Status = kyc.Status
        };
    }

    private async Task ProcessValidatedWebhookAsync(
        DojahWebhookData webhook,
        CancellationToken cancellationToken)
    {
        var correlation = await CorrelateWebhookAsync(
            webhook,
            cancellationToken);

        var lockedKyc = await unitOfWork.KycVerifications
            .GetByIdForUpdateAsync(
                correlation.Kyc.Id,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(KycVerification),
                correlation.Kyc.Id);

        correlation = new WebhookCorrelation(
            lockedKyc,
            correlation.DojahReference,
            correlation.IsLegacy);

        var kyc = correlation.Kyc;

        ValidateActiveAttemptReference(webhook, kyc);

        var attempt = await unitOfWork.KycVerificationAttempts
            .GetByCorrelationReferenceAsync(
                ProviderName,
                kyc.ProviderReference!,
                cancellationToken);
        var attemptCreated = attempt is null;
        attempt ??= CreateAttempt(kyc);
        if (attemptCreated)
        {
            await unitOfWork.KycVerificationAttempts.CreateAsync(
                attempt,
                cancellationToken);
        }

        var dojahReferenceChanged = false;

        if (!string.IsNullOrWhiteSpace(correlation.DojahReference))
        {
            var referenceOwner = await unitOfWork.KycVerifications
                .GetByDojahReferenceAsync(
                    correlation.DojahReference,
                    cancellationToken);

            if (referenceOwner is not null &&
                referenceOwner.Id != kyc.Id)
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
            !webhook.Status.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase))
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

        var previousStatus = kyc.Status;

        var nextStatus = MapStatus(
            webhook.Status,
            kyc.Status);

        var rejectionReason = nextStatus == KycStatus.Rejected
            ? SanitizeReason(
                webhook.Message,
                webhook.Status)
            : null;

        var resultCode = webhook.ResultCode ?? webhook.Status;

        if (!dojahReferenceChanged &&
            kyc.ProviderStatus == webhook.Status &&
            kyc.Status == nextStatus &&
            kyc.RejectionReason == rejectionReason &&
            attempt.ResultCode == resultCode)
        {
            if (attemptCreated)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

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
        kyc.VerifiedAt = nextStatus == KycStatus.Approved
            ? DateTime.UtcNow
            : null;

        attempt.ProviderReference = kyc.DojahReference;
        attempt.Status = ToProviderStatus(nextStatus);
        attempt.RawStatus = webhook.Status;
        attempt.ResultCode = resultCode;
        attempt.FailureReason = rejectionReason;
        attempt.ProviderUpdatedAt = kyc.LastProviderUpdatedAt;
        attempt.CompletedAt = nextStatus is KycStatus.Approved or KycStatus.Rejected
            ? DateTime.UtcNow
            : null;

        unitOfWork.KycVerifications.Update(kyc);
        unitOfWork.KycVerificationAttempts.Update(attempt);

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

        if (previousStatus != nextStatus &&
            nextStatus is KycStatus.Approved or KycStatus.Rejected)
        {
            var isApproved = nextStatus == KycStatus.Approved;

            await notificationService.TryCreateAsync(
                new CreateNotificationRequest
                {
                    UserId = kyc.UserId,
                    Type = isApproved
                        ? NotificationType.KycApproved
                        : NotificationType.KycRejected,
                    Title = isApproved
                        ? "KYC approved"
                        : "KYC rejected",
                    Message = isApproved
                        ? "Your identity verification was approved."
                        : string.IsNullOrWhiteSpace(rejectionReason)
                            ? "Your identity verification was rejected."
                            : $"Your identity verification was rejected: {rejectionReason}",
                    RelatedEntityId = kyc.Id,
                    RelatedEntityType = nameof(KycVerification),
                    DeduplicationKey = isApproved
                        ? $"kyc-approved:{kyc.Id}"
                        : $"kyc-rejected:{kyc.Id}"
                },
                cancellationToken);
        }
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled &&
            !string.Equals(
                _kycSettings?.ActiveProvider,
                ProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceUnavailableException(
                "Dojah verification is currently unavailable.");
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
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(privateKey)))
                .ToLowerInvariant();

            ValidateHexSignature(
                signatureV2,
                expectedV2);

            return;
        }

        if (!string.IsNullOrWhiteSpace(signatureV1))
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(privateKey));

            var expectedV1 = Convert.ToHexString(
                    hmac.ComputeHash(payload.ToArray()))
                .ToLowerInvariant();

            ValidateHexSignature(
                signatureV1,
                expectedV1);

            return;
        }

        throw new UnauthorizedException(
            "Invalid webhook signature.");
    }

    private static void ValidateHexSignature(
        string signature,
        string expected)
    {
        var supplied = signature.Trim();

        const string signaturePrefix = "sha256=";

        if (supplied.StartsWith(
                signaturePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            supplied = supplied[signaturePrefix.Length..].Trim();
        }

        if (supplied.Length != 64 ||
            !supplied.All(Uri.IsHexDigit))
        {
            throw new UnauthorizedException(
                "Invalid webhook signature.");
        }

        var suppliedBytes = Encoding.ASCII.GetBytes(
            supplied.ToLowerInvariant());

        var expectedBytes = Encoding.ASCII.GetBytes(expected);

        if (!CryptographicOperations.FixedTimeEquals(
                suppliedBytes,
                expectedBytes))
        {
            throw new UnauthorizedException(
                "Invalid webhook signature.");
        }
    }

    private static DojahWebhookData ParseWebhook(
        ReadOnlySpan<byte> payload)
    {
        var parsed = JsonSerializer.Deserialize<DojahWebhookPayload>(
                payload,
                WebhookSerializerOptions)
            ?? throw new JsonException(
                "Missing webhook payload.");

        var referenceId = ValidateRequiredReference(
            parsed.ReferenceId ??
            parsed.ReferenceIdSnakeCase ??
            parsed.Reference ??
            parsed.Metadata?.KycReference,
            "referenceId/reference_id/reference/metadata.kyc_reference");

        var status = ValidateRequiredReference(
            parsed.VerificationStatus ??
            parsed.VerificationStatusSnakeCase,
            "verificationStatus/verification_status");

        string? providerReference;

        /*
         * Dojah production may return a malformed metadata.kyc_reference
         * value containing an appended widgetId.
         *
         * When the root reference is a Pryde-generated reference, it is
         * the trusted value and must be used for correlation.
         */
        if (IsPrydeOwnedReference(referenceId))
        {
            providerReference = referenceId;
        }
        else
        {
            providerReference =
                ValidateOptionalReference(
                    parsed.VendorReference,
                    "vendor_reference") ??
                ValidateOptionalReference(
                    parsed.CustomerReference,
                    "customer_reference") ??
                ValidateOptionalReference(
                    parsed.CustomReference,
                    "custom_reference") ??
                ValidateOptionalReference(
                    parsed.Metadata?.KycReference,
                    "metadata.kyc_reference") ??
                ValidateOptionalReference(
                    parsed.Metadata?.VendorReference,
                    "metadata.vendor_reference") ??
                ValidateOptionalReference(
                    parsed.Metadata?.CustomerReference,
                    "metadata.customer_reference") ??
                ValidateOptionalReference(
                    parsed.Metadata?.CustomReference,
                    "metadata.custom_reference") ??
                ValidateOptionalReference(
                    parsed.Metadata?.ReferenceId,
                    "metadata.reference_id") ??
                ValidateOptionalReference(
                    parsed.Metadata?.UserId,
                    "metadata.user_id");
        }

        return new DojahWebhookData(
            referenceId,
            providerReference,
            status.Trim(),
            parsed.Message,
            ValidateOptionalReference(
                parsed.ResultCode ?? parsed.Code,
                "result_code/code"));
    }

    private static string ValidateRequiredReference(
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                $"Missing {propertyName}.");
        }

        return ValidateReferenceLength(
            value.Trim(),
            propertyName);
    }

    private static string? ValidateOptionalReference(
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ValidateReferenceLength(
            value.Trim(),
            propertyName);
    }

    private static string ValidateReferenceLength(
        string value,
        string propertyName)
    {
        if (value.Length > 100)
        {
            throw new JsonException(
                $"{propertyName} is too long.");
        }

        return value;
    }

    private static KycStatus MapStatus(
        string providerStatus,
        KycStatus currentStatus)
    {
        if (providerStatus.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase))
        {
            return KycStatus.Approved;
        }

        if (providerStatus.Equals(
                "Failed",
                StringComparison.OrdinalIgnoreCase) ||
            providerStatus.Equals(
                "Abandoned",
                StringComparison.OrdinalIgnoreCase))
        {
            return KycStatus.Rejected;
        }

        return currentStatus == KycStatus.Approved
            ? KycStatus.Approved
            : KycStatus.Pending;
    }

    private static string SanitizeReason(
        string? reason,
        string providerStatus)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return providerStatus.Equals(
                "Abandoned",
                StringComparison.OrdinalIgnoreCase)
                ? "Verification was abandoned."
                : "Dojah verification checks were not completed successfully.";
        }

        var sanitized = string.Join(
            ' ',
            reason.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));

        return sanitized.Length <= 500
            ? sanitized
            : sanitized[..500];
    }

    private static string CreateReference()
    {
        return $"PRYDE-{Guid.NewGuid():N}";
    }

    private static string CreateCorrelatedShareableLink(
        string shareableLink,
        string customReference)
    {
        var uriBuilder = new UriBuilder(shareableLink);

        var queryParts = new List<string>();

        foreach (var pair in uriBuilder.Query
                     .TrimStart('?')
                     .Split(
                         '&',
                         StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);

            if (key.Equals(
                    "reference_id",
                    StringComparison.OrdinalIgnoreCase) ||
                key.Equals(
                    "metadata[kyc_reference]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            queryParts.Add(pair);
        }

        queryParts.Add(
            $"{Uri.EscapeDataString("reference_id")}=" +
            $"{Uri.EscapeDataString(customReference)}");

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
                var existingByDojah = IsPrydeOwnedReference(
                    webhook.ReferenceId)
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

                return new WebhookCorrelation(
                    kyc,
                    null,
                    true);
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

            return new WebhookCorrelation(
                legacyKyc,
                null,
                true);
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

    private static bool IsPrydeOwnedReference(string reference)
    {
        return reference.StartsWith(
            "PRYDE-",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateActiveAttemptReference(
        DojahWebhookData webhook,
        KycVerification kyc)
    {
        var incomingProviderReference = webhook.ProviderReference;

        if (string.IsNullOrWhiteSpace(incomingProviderReference) &&
            IsPrydeOwnedReference(webhook.ReferenceId))
        {
            incomingProviderReference = webhook.ReferenceId;
        }

        if (!string.IsNullOrWhiteSpace(incomingProviderReference) &&
            !incomingProviderReference.Equals(
                kyc.ProviderReference,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "Dojah webhook reference does not match the active verification attempt.");
        }
    }

    private sealed record DojahWebhookData(
        string ReferenceId,
        string? ProviderReference,
        string Status,
        string? Message,
        string? ResultCode);

    private sealed class DojahWebhookPayload
    {
        [JsonPropertyName("referenceId")]
        public string? ReferenceId { get; init; }

        [JsonPropertyName("reference_id")]
        public string? ReferenceIdSnakeCase { get; init; }

        [JsonPropertyName("reference")]
        public string? Reference { get; init; }

        [JsonPropertyName("verificationStatus")]
        public string? VerificationStatus { get; init; }

        [JsonPropertyName("verification_status")]
        public string? VerificationStatusSnakeCase { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("result_code")]
        public string? ResultCode { get; init; }

        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("vendor_reference")]
        public string? VendorReference { get; init; }

        [JsonPropertyName("customer_reference")]
        public string? CustomerReference { get; init; }

        [JsonPropertyName("custom_reference")]
        public string? CustomReference { get; init; }

        [JsonPropertyName("metadata")]
        public DojahWebhookMetadata? Metadata { get; init; }
    }

    private sealed class DojahWebhookMetadata
    {
        [JsonPropertyName("kyc_reference")]
        public string? KycReference { get; init; }

        [JsonPropertyName("vendor_reference")]
        public string? VendorReference { get; init; }

        [JsonPropertyName("customer_reference")]
        public string? CustomerReference { get; init; }

        [JsonPropertyName("custom_reference")]
        public string? CustomReference { get; init; }

        [JsonPropertyName("reference_id")]
        public string? ReferenceId { get; init; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }
    }

    private sealed record WebhookCorrelation(
        KycVerification Kyc,
        string? DojahReference,
        bool IsLegacy);

    private static KycProviderResult ToProviderResult(
        DojahKycConfigResponseDto config) => new()
    {
        Provider = ProviderName,
        IntegrationType = "Widget",
        Reference = config.ProviderReference,
        Status = ToProviderStatus(config.Status),
        SessionUrl = config.ShareableLink,
        ClientConfiguration = new Dictionary<string, string>
        {
            ["appId"] = config.AppId,
            ["publicKey"] = config.PublicKey,
            ["widgetId"] = config.WidgetId
        },
        Metadata = config.Metadata
    };

    private async Task<bool> EnsureAttemptExistsAsync(
        KycVerification kyc,
        CancellationToken cancellationToken,
        DateTime? startedAt = null)
    {
        if (string.IsNullOrWhiteSpace(kyc.ProviderReference))
        {
            return false;
        }

        var existing = await unitOfWork.KycVerificationAttempts
            .GetByCorrelationReferenceAsync(
                ProviderName,
                kyc.ProviderReference,
                cancellationToken);

        if (existing is not null)
        {
            return false;
        }

        await unitOfWork.KycVerificationAttempts.CreateAsync(
            CreateAttempt(kyc, startedAt),
            cancellationToken);
        return true;
    }

    private static KycVerificationAttempt CreateAttempt(
        KycVerification kyc,
        DateTime? startedAt = null) => new()
    {
        KycVerificationId = kyc.Id,
        ProviderName = ProviderName,
        CorrelationReference = kyc.ProviderReference!,
        ProviderReference = kyc.DojahReference,
        Status = ToProviderStatus(kyc.Status),
        RawStatus = kyc.ProviderStatus,
        ResultCode = kyc.ProviderStatus,
        FailureReason = kyc.RejectionReason,
        StartedAt = startedAt ?? kyc.CreatedAt,
        ProviderUpdatedAt = kyc.LastProviderUpdatedAt,
        CompletedAt = kyc.Status is KycStatus.Approved or KycStatus.Rejected
            ? kyc.VerifiedAt ?? kyc.LastProviderUpdatedAt
            : null
    };

    private static KycProviderStatus ToProviderStatus(KycStatus status) =>
        status switch
        {
            KycStatus.Submitted => KycProviderStatus.Submitted,
            KycStatus.Approved => KycProviderStatus.Approved,
            KycStatus.Rejected => KycProviderStatus.Rejected,
            _ => KycProviderStatus.Pending
        };
}

public sealed class DojahKycService : IDojahKycService
{
    private readonly DojahKycProvider _provider;

    public DojahKycService(
        IUnitOfWork unitOfWork,
        IOptions<DojahSettings> options,
        ILogger<DojahKycService> logger)
    {
        _provider = new DojahKycProvider(
            unitOfWork,
            options,
            NullLogger<DojahKycProvider>.Instance,
            new NotificationService(unitOfWork));
    }

    internal DojahKycService(DojahKycProvider provider)
    {
        _provider = provider;
    }

    public Task<DojahKycConfigResponseDto> GetConfigAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _provider.GetConfigAsync(userId, cancellationToken);

    public Task<DojahKycConfigResponseDto> RetryAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _provider.RetryAsync(userId, cancellationToken);

    public Task ProcessWebhookAsync(ReadOnlyMemory<byte> payload, string? signatureV1, string? signatureV2, CancellationToken cancellationToken = default) =>
        _provider.ProcessWebhookAsync(payload, signatureV1, signatureV2, cancellationToken);
}
