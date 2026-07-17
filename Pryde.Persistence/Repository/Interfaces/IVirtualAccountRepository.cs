using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IVirtualAccountRepository
{
    Task<VirtualAccount?> GetByWalletIdAsync(
        Guid walletId,
        CancellationToken cancellationToken = default);

    Task<VirtualAccount?> GetByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<VirtualAccount> CreateAsync(
        VirtualAccount virtualAccount,
        CancellationToken cancellationToken = default);
}
