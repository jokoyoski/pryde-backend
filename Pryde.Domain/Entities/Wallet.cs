using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class Wallet : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public decimal Balance { get; set; }
        public decimal EscrowBalance { get; set; }
    }
}