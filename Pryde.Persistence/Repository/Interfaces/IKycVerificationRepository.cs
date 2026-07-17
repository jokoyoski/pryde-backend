using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IKycVerificationRepository
{
    Task<KycVerification?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<KycVerification?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<KycVerification?> GetByProviderReferenceAsync(
        string providerReference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KycVerification>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<KycVerification> CreateAsync(
        KycVerification kycVerification,
        CancellationToken cancellationToken = default);

    void Update(KycVerification kycVerification);

    void Delete(KycVerification kycVerification);
    Task<IReadOnlyList<KycVerification>> GetByStatusAsync(KycStatus status, CancellationToken cancellationToken = default);
}
