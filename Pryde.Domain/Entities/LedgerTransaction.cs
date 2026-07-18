using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class LedgerTransaction : BaseEntity
{
    public string Reference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public LedgerTransactionType TransactionType { get; set; }
    public LedgerTransactionStatus Status { get; set; } = LedgerTransactionStatus.Posted;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public Guid? BookingId { get; set; }
    public Guid? EscrowId { get; set; }
    public Escrow? Escrow { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CompletedAt { get; set; }
    public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
}
