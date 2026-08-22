using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class KycVerificationResponseDto : WorkflowResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? BiometricVerificationUrl { get; set; }
    public string? DriverLicenseUrl { get; set; }
    public string? SecondaryIdentificationUrl { get; set; }
    public KycStatus Status { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderReference { get; set; }
    public string? DojahReference { get; set; }
    public string? ProviderStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? LastProviderUpdatedAt { get; set; }
    public string? SelectedIdType { get; set; }
    public string? VerificationMethod { get; set; }
    public bool CallbackConfirmed { get; set; }
    public bool CanRetry { get; set; }
    public KycAttemptAllowanceResponseDto AttemptAllowance { get; set; } = new();
    public IReadOnlyList<KycFlowStatusResponseDto> Flows { get; set; } = [];
}

public sealed class KycFlowStatusResponseDto
{
    public string Flow { get; set; } = string.Empty;
    public bool Required { get; set; }
    public KycProviderStatus Status { get; set; }
    public string? RawStatus { get; set; }
    public string? ResultCode { get; set; }
    public string? FailureReason { get; set; }
    public string? IdType { get; set; }
    public string? VerificationMethod { get; set; }
    public bool CallbackConfirmed { get; set; }
    public bool CanRetry { get; set; }
}
