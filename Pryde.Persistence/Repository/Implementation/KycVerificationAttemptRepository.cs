using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class KycVerificationAttemptRepository(PrydeDbContext context) : IKycVerificationAttemptRepository
{
    public Task<KycVerificationAttempt?> GetByCorrelationReferenceAsync(string providerName, string correlationReference, CancellationToken cancellationToken = default) =>
        context.KycVerificationAttempts.FirstOrDefaultAsync(x => x.ProviderName == providerName && x.CorrelationReference == correlationReference, cancellationToken);

    public Task<KycVerificationAttempt?> GetByCorrelationReferenceForUpdateAsync(
        string providerName,
        string correlationReference,
        CancellationToken cancellationToken = default) =>
        context.KycVerificationAttempts
            .FromSqlInterpolated($"""
                SELECT *
                FROM "KycVerificationAttempts"
                WHERE "ProviderName" = {providerName}
                  AND "CorrelationReference" = {correlationReference}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<KycVerificationAttempt?> GetByProviderReferenceAsync(string providerName, string providerReference, CancellationToken cancellationToken = default) =>
        context.KycVerificationAttempts.FirstOrDefaultAsync(x => x.ProviderName == providerName && x.ProviderReference == providerReference, cancellationToken);

    public async Task<IReadOnlyList<KycVerificationAttempt>> GetByKycVerificationIdAsync(Guid kycVerificationId, CancellationToken cancellationToken = default) =>
        await context.KycVerificationAttempts.AsNoTracking().Where(x => x.KycVerificationId == kycVerificationId).OrderBy(x => x.StartedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetDistinctAttemptReferencesAsync(
        Guid kycVerificationId,
        DateTime startedAtInclusive,
        DateTime startedAtExclusive,
        CancellationToken cancellationToken = default) =>
        await context.KycVerificationAttempts
            .AsNoTracking()
            .Where(x =>
                x.KycVerificationId == kycVerificationId &&
                !x.IsDeleted &&
                x.StartedAt >= startedAtInclusive &&
                x.StartedAt < startedAtExclusive &&
                (x.RawStatus == null || x.RawStatus != "LinkCreationFailed"))
            .Select(x => x.AttemptGroupReference ?? x.CorrelationReference)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<KycVerificationAttempt> CreateAsync(KycVerificationAttempt attempt, CancellationToken cancellationToken = default)
    {
        await context.KycVerificationAttempts.AddAsync(attempt, cancellationToken);
        return attempt;
    }

    public void Update(KycVerificationAttempt attempt) => context.KycVerificationAttempts.Update(attempt);
}
