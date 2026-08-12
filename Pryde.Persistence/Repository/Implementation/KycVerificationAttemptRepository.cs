using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class KycVerificationAttemptRepository(PrydeDbContext context) : IKycVerificationAttemptRepository
{
    public Task<KycVerificationAttempt?> GetByCorrelationReferenceAsync(string providerName, string correlationReference, CancellationToken cancellationToken = default) =>
        context.KycVerificationAttempts.FirstOrDefaultAsync(x => x.ProviderName == providerName && x.CorrelationReference == correlationReference, cancellationToken);

    public Task<KycVerificationAttempt?> GetByProviderReferenceAsync(string providerName, string providerReference, CancellationToken cancellationToken = default) =>
        context.KycVerificationAttempts.FirstOrDefaultAsync(x => x.ProviderName == providerName && x.ProviderReference == providerReference, cancellationToken);

    public async Task<IReadOnlyList<KycVerificationAttempt>> GetByKycVerificationIdAsync(Guid kycVerificationId, CancellationToken cancellationToken = default) =>
        await context.KycVerificationAttempts.AsNoTracking().Where(x => x.KycVerificationId == kycVerificationId).OrderBy(x => x.StartedAt).ToListAsync(cancellationToken);

    public async Task<KycVerificationAttempt> CreateAsync(KycVerificationAttempt attempt, CancellationToken cancellationToken = default)
    {
        await context.KycVerificationAttempts.AddAsync(attempt, cancellationToken);
        return attempt;
    }

    public void Update(KycVerificationAttempt attempt) => context.KycVerificationAttempts.Update(attempt);
}
