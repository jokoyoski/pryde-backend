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

    public Task<WalletTransaction?> GetByProviderReferenceAsync(
        string provider,
        string reference,
        CancellationToken cancellationToken = default)
    {
        return context.WalletTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.Provider == provider &&
                    transaction.Reference == reference,
                cancellationToken);
    }

    public Task<WalletTransaction?>
        GetWithdrawalByProviderReferenceForUpdateAsync(
            string reference,
            CancellationToken cancellationToken = default)
    {
        return context.WalletTransactions
            .Include(transaction => transaction.Wallet)
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.Type == WalletTransactionType.Withdrawal &&
                    transaction.Provider == "Paystack" &&
                    transaction.Reference == reference,
                cancellationToken);
    }

    public async Task<(
        IReadOnlyList<WalletTransaction> Items,
        int TotalCount)> GetByWalletIdAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(transaction => transaction.WalletId == walletId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<decimal> SumByUserIdAndTypeAsync(
        Guid userId,
        WalletTransactionType transactionType,
        DateTime? createdFrom,
        DateTime? createdTo,
        CancellationToken cancellationToken = default)
    {
        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Wallet.UserId == userId &&
                transaction.Type == transactionType);

        if (createdFrom.HasValue)
        {
            query = query.Where(transaction =>
                transaction.CreatedAt >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(transaction =>
                transaction.CreatedAt <= createdTo.Value);
        }

        return await query.SumAsync(
            transaction => (decimal?)transaction.Amount,
            cancellationToken) ?? 0m;
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
