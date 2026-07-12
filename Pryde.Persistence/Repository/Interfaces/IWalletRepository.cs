using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Wallet> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
}