using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class EscrowResponseDto
{
    public Guid EscrowId { get; set; }
    public Guid BookingId { get; set; }
    public Guid PassengerId { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal DriverAmount { get; set; }
    public decimal PlatformAmount { get; set; }
    public string Currency { get; set; } = "NGN";
    public EscrowStatus Status { get; set; }
    public DateTime HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}

public class LedgerEntryResponseDto
{
    public Guid Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public LedgerAccountType AccountType { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
}

public class LedgerTransactionResponseDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public LedgerTransactionType TransactionType { get; set; }
    public LedgerTransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public Guid? BookingId { get; set; }
    public Guid? EscrowId { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class LedgerTransactionDetailResponseDto : LedgerTransactionResponseDto
{
    public IReadOnlyList<LedgerEntryResponseDto> Entries { get; set; } = [];
}

public class FinancialSummaryResponseDto
{
    public string Currency { get; set; } = "NGN";
    public decimal TotalPlatformEarnings { get; set; }
    public decimal MonthlyPlatformEarnings { get; set; }
    public decimal TotalEscrowHeld { get; set; }
    public decimal TotalEscrowReleased { get; set; }
    public decimal TotalEscrowRefunded { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal TotalDriverPayouts { get; set; }
}
