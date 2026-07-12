using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IWalletTransactionRepository
{
    Task<WalletTransaction> CreateAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default);
}