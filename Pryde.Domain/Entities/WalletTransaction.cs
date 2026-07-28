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
        public WalletTransactionStatus? Status { get; set; }
        public string? Description { get; set; }
        public string? Provider { get; set; }
        public string? Currency { get; set; }
        public string? BankName { get; set; }
        public string? MaskedAccountNumber { get; set; }
        public string? AccountName { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
