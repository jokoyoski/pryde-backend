using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class LedgerAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LedgerAccountType AccountType { get; set; }
    public Guid? WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public string Currency { get; set; } = "NGN";
    public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
}
