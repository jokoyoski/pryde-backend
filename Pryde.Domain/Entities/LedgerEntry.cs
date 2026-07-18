using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class LedgerEntry : BaseEntity
{
    public Guid LedgerTransactionId { get; set; }
    public LedgerTransaction LedgerTransaction { get; set; } = null!;
    public Guid LedgerAccountId { get; set; }
    public LedgerAccount LedgerAccount { get; set; } = null!;
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
}
