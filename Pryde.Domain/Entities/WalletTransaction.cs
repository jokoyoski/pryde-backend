using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class WalletTransaction : BaseEntity
    {
        public Guid WalletId { get; set; }
        public Wallet Wallet { get; set; } = null!;
        public decimal Amount { get; set; }
        public WalletTransactionType Type { get; set; }
        public string? Reference { get; set; }
    }
}