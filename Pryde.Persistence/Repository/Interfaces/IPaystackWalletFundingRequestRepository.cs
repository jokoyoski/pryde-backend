using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IPaystackWalletFundingRequestRepository
{
    Task<PaystackWalletFundingRequest> CreateAsync(
        PaystackWalletFundingRequest fundingRequest,
        CancellationToken cancellationToken = default);
    Task<PaystackWalletFundingRequest?> GetByReferenceAsync(
        string reference,
        CancellationToken cancellationToken = default);
    Task<PaystackWalletFundingRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<PaystackWalletFundingRequest?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
