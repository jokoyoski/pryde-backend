using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
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

    public async Task<IReadOnlyList<WalletTransaction>> GetWithdrawalsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.WalletTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Wallet.UserId == userId &&
                transaction.Type == WalletTransactionType.Withdrawal)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Select(transaction => new WalletTransaction
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Reference = transaction.Reference,
                Status = transaction.Status,
                Currency = transaction.Currency,
                BankName = transaction.BankName,
                MaskedAccountNumber = transaction.MaskedAccountNumber,
                AccountName = transaction.AccountName,
                CreatedAt = transaction.CreatedAt,
                CompletedAt = transaction.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<WalletTransaction?> GetWithdrawalByIdAndUserIdAsync(
        Guid withdrawalId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.WalletTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Id == withdrawalId &&
                transaction.Wallet.UserId == userId &&
                transaction.Type == WalletTransactionType.Withdrawal)
            .Select(transaction => new WalletTransaction
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Reference = transaction.Reference,
                Status = transaction.Status,
                Currency = transaction.Currency,
                BankName = transaction.BankName,
                MaskedAccountNumber = transaction.MaskedAccountNumber,
                AccountName = transaction.AccountName,
                CreatedAt = transaction.CreatedAt,
                CompletedAt = transaction.CompletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
