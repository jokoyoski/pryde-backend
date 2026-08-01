using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IWalletTransactionRepository
{
    Task<WalletTransaction> CreateAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    Task<(
        IReadOnlyList<WalletTransaction> Items,
        int TotalCount)> GetByWalletIdAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    Task<decimal> SumByUserIdAndTypeAsync(
        Guid userId,
        WalletTransactionType transactionType,
        DateTime? createdFrom,
        DateTime? createdTo,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetWithdrawalsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<WalletTransaction?> GetWithdrawalByIdAndUserIdAsync(
        Guid withdrawalId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
