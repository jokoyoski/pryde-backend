using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class KycVerificationResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? BiometricVerificationUrl { get; set; }
    public string? DriverLicenseUrl { get; set; }
    public string? SecondaryIdentificationUrl { get; set; }
    public KycStatus Status { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
