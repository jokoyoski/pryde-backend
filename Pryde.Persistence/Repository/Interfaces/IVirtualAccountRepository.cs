using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IVirtualAccountRepository
{
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
