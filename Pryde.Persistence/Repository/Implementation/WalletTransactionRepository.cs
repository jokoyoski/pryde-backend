using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class WalletTransactionRepository(PrydeDbContext context) : IWalletTransactionRepository
{
    public async Task<WalletTransaction> CreateAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
    {
        await context.WalletTransactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        return await context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}