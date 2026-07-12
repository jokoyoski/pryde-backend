using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class WalletRepository(PrydeDbContext context) : IWalletRepository
{
    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
    }

    public async Task<Wallet> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        await context.Wallets.AddAsync(wallet, cancellationToken);
        return wallet;
    }

    public void Update(Wallet wallet) => context.Wallets.Update(wallet);
}