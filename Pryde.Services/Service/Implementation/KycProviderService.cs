using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public sealed class KycProviderService(
    IKycProviderResolver resolver,
    IUnitOfWork unitOfWork,
    ILogger<KycProviderService> logger,
    IOptions<KycSettings>? kycOptions = null) : IKycProviderService
{
    private readonly KycSettings _kycSettings =
        kycOptions?.Value ?? new KycSettings();

    public async Task<KycProviderResult> CreateSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await CreateSessionAsync(userId, null, cancellationToken);

    public async Task<KycProviderResult> CreateSessionAsync(
        Guid userId,
        string? selectedIdType,
        CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);
        if (kyc?.Status == KycStatus.Approved)
        {
            return await AddAttemptAllowanceAsync(
                CreateValidatedStatusResult(kyc, KycProviderStatus.Approved),
                kyc,
                cancellationToken);
        }

        var recoveredKyc = kyc is null
            ? null
            : await MakeIncompleteSmileAttemptRetryableAsync(
                kyc,
                false,
                cancellationToken);
        if (recoveredKyc is not null)
        {
            return await AddAttemptAllowanceAsync(
                CreateValidatedStatusResult(
                    recoveredKyc,
                    KycProviderStatus.Rejected),
                recoveredKyc,
                cancellationToken);
        }

        var provider = kyc is null
            ? resolver.ResolveActive()
            : await ResolveOwnerAsync(kyc, cancellationToken);
        var result = await provider.CreateSessionAsync(
            new KycProviderRequest(userId, selectedIdType),
            cancellationToken);
        KycProviderResultInvariant.Ensure(provider.Name, result);
        return await AddAttemptAllowanceAsync(
            result,
            await unitOfWork.KycVerifications.GetByUserIdAsync(
                userId,
                cancellationToken),
            cancellationToken);
    }

    public async Task<KycProviderResult> RetryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);
        if (kyc?.Status == KycStatus.Approved)
        {
            return await AddAttemptAllowanceAsync(
                CreateValidatedStatusResult(kyc, KycProviderStatus.Approved),
                kyc,
                cancellationToken);
        }

        if (kyc is not null)
        {
            await MakeIncompleteSmileAttemptRetryableAsync(
                kyc,
                true,
                cancellationToken);
        }

        var provider = resolver.ResolveActive();
        var result = await provider.RetryAsync(
            new KycProviderRequest(userId),
            cancellationToken);
        KycProviderResultInvariant.Ensure(provider.Name, result);
        return await AddAttemptAllowanceAsync(
            result,
            await unitOfWork.KycVerifications.GetByUserIdAsync(
                userId,
                cancellationToken),
            cancellationToken);
    }

    private async Task<IKycProvider> ResolveOwnerAsync(
        KycVerification kyc,
        CancellationToken cancellationToken)
    {
        var attempts = await unitOfWork.KycVerificationAttempts
            .GetByKycVerificationIdAsync(kyc.Id, cancellationToken);
        var attemptProviders = attempts
            .Where(attempt =>
                attempt.Status is KycProviderStatus.Pending or KycProviderStatus.Submitted &&
                (string.IsNullOrWhiteSpace(kyc.ProviderReference) ||
                 string.Equals(
                     attempt.AttemptGroupReference,
                     kyc.ProviderReference,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     attempt.CorrelationReference,
                     kyc.ProviderReference,
                     StringComparison.Ordinal)))
            .Select(attempt => attempt.ProviderName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var providerName = IsSmileReference(kyc.ProviderReference)
            ? SmileIdKycProvider.ProviderName
            : attemptProviders.Count == 1
                ? attemptProviders[0]
                : kyc.ProviderName;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return resolver.ResolveActive();
        }

        if (!string.Equals(
                kyc.ProviderName,
                providerName,
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Correcting KYC provider ownership for verification {KycVerificationId} to {ProviderName}.",
                kyc.Id,
                providerName);
            kyc.ProviderName = providerName;
            unitOfWork.KycVerifications.Update(kyc);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return resolver.Resolve(providerName);
    }

    private async Task<KycVerification?> MakeIncompleteSmileAttemptRetryableAsync(
        KycVerification kyc,
        bool enforceAttemptLimit,
        CancellationToken cancellationToken)
    {
        if (kyc.Status is KycStatus.Approved or KycStatus.Rejected ||
            !IsSmileReference(kyc.ProviderReference))
        {
            return null;
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
        {
            var lockedKyc = await unitOfWork.KycVerifications
                .GetByIdForUpdateAsync(kyc.Id, transactionToken);
            if (lockedKyc is null ||
                lockedKyc.Status is KycStatus.Approved or KycStatus.Rejected ||
                !IsSmileReference(lockedKyc.ProviderReference))
            {
                return null;
            }

            var attempts = await unitOfWork.KycVerificationAttempts
                .GetByKycVerificationIdAsync(lockedKyc.Id, transactionToken);
            var incomplete = attempts
                .Where(attempt =>
                    IsCurrentPendingSmileAttempt(attempt, lockedKyc) &&
                    (string.IsNullOrWhiteSpace(attempt.VerificationUrl) ||
                     IsMissingLegacyIdentityData(attempt)))
                .ToList();
            if (incomplete.Count == 0)
            {
                return null;
            }

            if (enforceAttemptLimit)
            {
                await KycAttemptAllowanceCalculator.EnsureCanCreateAsync(
                    unitOfWork,
                    lockedKyc.Id,
                    $"preflight-{Guid.NewGuid():N}",
                    _kycSettings,
                    DateTime.UtcNow,
                    transactionToken);
            }

            var now = DateTime.UtcNow;
            var hasMissingIdentityData = false;
            foreach (var attempt in incomplete)
            {
                var missingIdentityData = IsMissingLegacyIdentityData(attempt);
                hasMissingIdentityData |= missingIdentityData;
                attempt.Status = KycProviderStatus.Rejected;
                if (missingIdentityData)
                {
                    attempt.ResultCode =
                        SmileIdKycProvider.LegacyIdentityDataMissingStatus;
                    attempt.FailureReason =
                        "The legacy Smile ID attempt is missing identity metadata.";
                }
                else
                {
                    attempt.RawStatus = "LinkCreationIncomplete";
                    attempt.FailureReason =
                        "Smile ID link creation was not completed.";
                }
                attempt.ProviderUpdatedAt = now;
                attempt.CompletedAt = now;
                unitOfWork.KycVerificationAttempts.Update(attempt);
            }

            var recoveryStatus = hasMissingIdentityData
                ? SmileIdKycProvider.LegacyIdentityDataMissingStatus
                : "LinkCreationIncomplete";
            lockedKyc.Status = KycStatus.Rejected;
            lockedKyc.ProviderName = SmileIdKycProvider.ProviderName;
            lockedKyc.ProviderStatus = recoveryStatus;
            lockedKyc.RejectionReason = hasMissingIdentityData
                ? "The legacy Smile ID attempt is missing identity metadata."
                : "Smile ID link creation was not completed.";
            lockedKyc.LastProviderUpdatedAt = now;
            unitOfWork.KycVerifications.Update(lockedKyc);
            await unitOfWork.SaveChangesAsync(transactionToken);
            return lockedKyc;
        }, cancellationToken);
    }

    private static bool IsCurrentPendingSmileAttempt(
        KycVerificationAttempt attempt,
        KycVerification kyc) =>
        string.Equals(
            attempt.ProviderName,
            SmileIdKycProvider.ProviderName,
            StringComparison.OrdinalIgnoreCase) &&
        attempt.Status is KycProviderStatus.Pending or KycProviderStatus.Submitted &&
        (string.IsNullOrWhiteSpace(attempt.AttemptGroupReference) ||
         string.Equals(
             attempt.AttemptGroupReference,
             kyc.ProviderReference,
             StringComparison.Ordinal));

    private static bool IsMissingLegacyIdentityData(
        KycVerificationAttempt attempt) =>
        SmileIdKycProvider.CanonicalFlow(attempt.FlowType) ==
            SmileIdKycProvider.IdentityFlow &&
        !SmileIdKycProvider.HasUsableIdentityOptions(
            attempt.IdentityOptions) &&
        string.IsNullOrWhiteSpace(attempt.IdentityType) &&
        string.IsNullOrWhiteSpace(attempt.VerificationMethod);

    private static KycProviderResult CreateValidatedStatusResult(
        KycVerification kyc,
        KycProviderStatus status)
    {
        var result = new KycProviderResult
        {
            Provider = string.IsNullOrWhiteSpace(kyc.ProviderName)
                ? KycSettings.DefaultProvider
                : kyc.ProviderName,
            Reference = kyc.ProviderReference ?? string.Empty,
            Status = status
        };
        KycProviderResultInvariant.Ensure(result.Provider, result);
        return result;
    }

    private async Task<KycProviderResult> AddAttemptAllowanceAsync(
        KycProviderResult result,
        KycVerification? kyc,
        CancellationToken cancellationToken)
    {
        if (kyc is null)
        {
            result.AttemptAllowance =
                KycAttemptAllowanceCalculator.CreateEmpty(
                    _kycSettings,
                    DateTime.UtcNow);
            return result;
        }

        result.AttemptAllowance = await KycAttemptAllowanceCalculator.GetAsync(
            unitOfWork,
            kyc.Id,
            _kycSettings,
            DateTime.UtcNow,
            cancellationToken);
        return result;
    }

    private static bool IsSmileReference(string? reference) =>
        reference?.StartsWith(
            "SMILE-GROUP-",
            StringComparison.OrdinalIgnoreCase) == true;
}
