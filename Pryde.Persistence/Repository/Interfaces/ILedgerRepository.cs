using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ILedgerRepository
{
    Task<LedgerAccount?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<LedgerTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<LedgerTransaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetTransactionsAsync(LedgerTransactionType? transactionType, LedgerTransactionStatus? status, string? reference, Guid? bookingId, Guid? escrowId, Guid? userId, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<LedgerFinancialTotals> GetFinancialTotalsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerRevenueTotal>> GetRevenueSummaryAsync(DateTime dateFrom, CancellationToken cancellationToken = default);
    Task<LedgerAccount> CreateAsync(LedgerAccount account, CancellationToken cancellationToken = default);
    Task<LedgerTransaction> CreateAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default);
    Task<LedgerEntry> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}

public sealed record LedgerFinancialTotals(decimal PlatformEarnings, decimal MonthlyPlatformEarnings, decimal DriverPayouts, int TotalTransactions);
public sealed record LedgerRevenueTotal(DateTime Date, decimal Amount);
