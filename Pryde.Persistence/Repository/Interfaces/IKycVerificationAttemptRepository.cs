using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IKycVerificationAttemptRepository
{
    Task<KycVerificationAttempt?> GetByCorrelationReferenceAsync(string providerName, string correlationReference, CancellationToken cancellationToken = default);
    Task<KycVerificationAttempt?> GetByProviderReferenceAsync(string providerName, string providerReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KycVerificationAttempt>> GetByKycVerificationIdAsync(Guid kycVerificationId, CancellationToken cancellationToken = default);
    Task<KycVerificationAttempt> CreateAsync(KycVerificationAttempt attempt, CancellationToken cancellationToken = default);
    void Update(KycVerificationAttempt attempt);
}
