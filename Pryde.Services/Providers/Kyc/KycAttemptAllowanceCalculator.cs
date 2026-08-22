using Pryde.Contracts.ResponseModels;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.Kyc;

public static class KycAttemptAllowanceCalculator
{
    public static KycAttemptAllowanceResponseDto CreateEmpty(
        KycSettings settings,
        DateTime utcNow)
    {
        var monthStart = MonthStart(utcNow);
        return Create(
            settings.MaxAttemptsPerMonth,
            0,
            monthStart.AddMonths(1));
    }

    public static async Task<KycAttemptAllowanceResponseDto> GetAsync(
        IUnitOfWork unitOfWork,
        Guid kycVerificationId,
        KycSettings settings,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var monthStart = MonthStart(utcNow);
        var resetsAt = monthStart.AddMonths(1);
        var references = await unitOfWork.KycVerificationAttempts
            .GetDistinctAttemptReferencesAsync(
                kycVerificationId,
                monthStart,
                resetsAt,
                cancellationToken);

        return Create(settings.MaxAttemptsPerMonth, references.Count, resetsAt);
    }

    public static async Task EnsureCanCreateAsync(
        IUnitOfWork unitOfWork,
        Guid kycVerificationId,
        string attemptReference,
        KycSettings settings,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var monthStart = MonthStart(utcNow);
        var resetsAt = monthStart.AddMonths(1);
        var references = await unitOfWork.KycVerificationAttempts
            .GetDistinctAttemptReferencesAsync(
                kycVerificationId,
                monthStart,
                resetsAt,
                cancellationToken);

        if (references.Contains(attemptReference, StringComparer.Ordinal))
        {
            return;
        }

        var allowance = Create(
            settings.MaxAttemptsPerMonth,
            references.Count,
            resetsAt);
        if (!allowance.CanAttempt)
        {
            throw new KycAttemptLimitExceededException(allowance);
        }
    }

    private static DateTime MonthStart(DateTime utcNow)
    {
        var normalized = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : utcNow.ToUniversalTime();
        return new DateTime(
            normalized.Year,
            normalized.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    private static KycAttemptAllowanceResponseDto Create(
        int limit,
        int used,
        DateTime resetsAt)
    {
        var remaining = Math.Max(0, limit - used);
        return new KycAttemptAllowanceResponseDto
        {
            Limit = limit,
            Used = used,
            Remaining = remaining,
            CanAttempt = remaining > 0,
            ResetsAt = resetsAt,
            Description = remaining switch
            {
                0 => "You have no KYC attempts remaining this month. You can try again next month.",
                1 => "You have 1 KYC attempt remaining this month.",
                _ => $"You have {remaining} KYC attempts remaining this month."
            }
        };
    }
}
