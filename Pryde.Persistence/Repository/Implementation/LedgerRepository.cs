using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class LedgerRepository(PrydeDbContext context) : ILedgerRepository
{
    public Task<LedgerAccount?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var local = context.LedgerAccounts.Local.FirstOrDefault(account => account.Code == code);
        return local is null
            ? context.LedgerAccounts.FirstOrDefaultAsync(account => account.Code == code, cancellationToken)
            : Task.FromResult<LedgerAccount?>(local);
    }

    public Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        context.LedgerTransactions.AsNoTracking()
            .FirstOrDefaultAsync(transaction => transaction.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<LedgerTransaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.LedgerTransactions.AsNoTracking()
            .Include(transaction => transaction.Entries).ThenInclude(entry => entry.LedgerAccount)
            .FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetTransactionsAsync(
        LedgerTransactionType? transactionType, LedgerTransactionStatus? status, string? reference,
        Guid? bookingId, Guid? escrowId, Guid? userId, DateTime? dateFrom, DateTime? dateTo,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.LedgerTransactions.AsNoTracking().AsQueryable();
        if (transactionType.HasValue) query = query.Where(transaction => transaction.TransactionType == transactionType.Value);
        if (status.HasValue) query = query.Where(transaction => transaction.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var term = reference.Trim().ToLower();
            query = query.Where(transaction => transaction.Reference.ToLower().Contains(term));
        }
        if (bookingId.HasValue) query = query.Where(transaction => transaction.BookingId == bookingId.Value);
        if (escrowId.HasValue) query = query.Where(transaction => transaction.EscrowId == escrowId.Value);
        if (userId.HasValue) query = query.Where(transaction => transaction.Entries.Any(entry =>
            entry.LedgerAccount.Wallet != null && entry.LedgerAccount.Wallet.UserId == userId.Value));
        if (dateFrom.HasValue) query = query.Where(transaction => transaction.CompletedAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue) query = query.Where(transaction => transaction.CompletedAt <= dateTo.Value.ToUniversalTime());
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(transaction => transaction.CompletedAt).ThenBy(transaction => transaction.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<LedgerFinancialTotals> GetFinancialTotalsAsync(CancellationToken cancellationToken = default)
    {
        var platformEntries = context.LedgerEntries.AsNoTracking().Where(entry =>
            entry.LedgerAccount.AccountType == LedgerAccountType.PlatformRevenue &&
            entry.EntryType == LedgerEntryType.Credit);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var platform = await platformEntries.SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0;
        var monthly = await platformEntries.Where(entry => entry.CreatedAt >= monthStart)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0;
        var payouts = await context.LedgerEntries.AsNoTracking().Where(entry =>
                entry.LedgerTransaction.TransactionType == LedgerTransactionType.EscrowRelease &&
                entry.LedgerAccount.AccountType == LedgerAccountType.Wallet &&
                entry.EntryType == LedgerEntryType.Credit)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0;
        var count = await context.LedgerTransactions.CountAsync(cancellationToken);
        return new LedgerFinancialTotals(platform, monthly, payouts, count);
    }

    public async Task<IReadOnlyList<LedgerRevenueTotal>> GetRevenueSummaryAsync(
        DateTime dateFrom, CancellationToken cancellationToken = default)
    {
        return await context.LedgerEntries.AsNoTracking()
            .Where(entry => entry.CreatedAt >= dateFrom &&
                entry.LedgerAccount.AccountType == LedgerAccountType.PlatformRevenue &&
                entry.EntryType == LedgerEntryType.Credit)
            .GroupBy(entry => entry.CreatedAt.Date)
            .Select(group => new LedgerRevenueTotal(group.Key, group.Sum(entry => entry.Amount)))
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<LedgerAccount> CreateAsync(LedgerAccount account, CancellationToken cancellationToken = default)
    {
        await context.LedgerAccounts.AddAsync(account, cancellationToken);
        return account;
    }

    public async Task<LedgerTransaction> CreateAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default)
    {
        await context.LedgerTransactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<LedgerEntry> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await context.LedgerEntries.AddAsync(entry, cancellationToken);
        return entry;
    }
}
