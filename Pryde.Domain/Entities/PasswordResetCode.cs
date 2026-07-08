using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class PasswordResetCode : BaseEntity
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsUsed => UsedAt is not null;
        public bool IsValid => !IsExpired && !IsUsed;
    }
}