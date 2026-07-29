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
}
