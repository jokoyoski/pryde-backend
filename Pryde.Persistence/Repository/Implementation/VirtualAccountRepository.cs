using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class VirtualAccountRepository(PrydeDbContext context) : IVirtualAccountRepository
{
    public async Task<VirtualAccount?> GetByWalletIdAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        return await context.VirtualAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.WalletId == walletId, cancellationToken);
    }

    public async Task<VirtualAccount?> GetByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await context.VirtualAccounts
            .Include(x => x.Wallet)
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber, cancellationToken);
    }

    public async Task<bool> ExistsByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await context.VirtualAccounts
            .AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken);
    }

    public async Task<VirtualAccount> CreateAsync(
        VirtualAccount virtualAccount,
        CancellationToken cancellationToken = default)
    {
        await context.VirtualAccounts.AddAsync(virtualAccount, cancellationToken);
        return virtualAccount;
    }
}
