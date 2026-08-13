using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class KycVerificationAttempt : BaseEntity
{
    public Guid KycVerificationId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string CorrelationReference { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public KycProviderStatus Status { get; set; } = KycProviderStatus.Pending;
    public string? RawStatus { get; set; }
    public string? ResultCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProviderUpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public KycVerification KycVerification { get; set; } = null!;
}
