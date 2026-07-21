using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class VerificationCode : BaseEntity
{
    public Guid UserId { get; set; }
    public VerificationCodePurpose Purpose { get; set; }
    public VerificationChannel Channel { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime LastSentAt { get; set; }
    public User User { get; set; } = null!;
}
