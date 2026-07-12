using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class VirtualAccount : BaseEntity
    {
        public Guid WalletId { get; set; }
        public string BankName { get; set; } = default!;
        public string AccountName { get; set; } = default!;
        public string AccountNumber { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public Wallet Wallet { get; set; } = default!;
    }
}
