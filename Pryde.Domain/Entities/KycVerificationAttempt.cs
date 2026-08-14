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
    public string? ResultText { get; set; }
    public string? FlowType { get; set; }
    public string? AttemptGroupReference { get; set; }
    public string? ExternalUserReference { get; set; }
    public bool SmileActionSucceeded { get; set; }
    public bool SmileIdentitySucceeded { get; set; }
    public DateTime? ProviderEventTimestamp { get; set; }
    public string? CallbackPayloadHash { get; set; }
    public string? VerificationUrl { get; set; }
    public string? IdentityType { get; set; }
    public string? VerificationMethod { get; set; }
    public string? IdentityOptions { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProviderUpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public KycVerification KycVerification { get; set; } = null!;
}
