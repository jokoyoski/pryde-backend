using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public sealed class OnboardingStatusResponseDto
{
    public IReadOnlyList<string> Roles { get; set; } = [];
    public OnboardingStage CurrentStage { get; set; }
    public IReadOnlyList<OnboardingStage> CompletedStages { get; set; } = [];
    public OnboardingStage? NextStage { get; set; }
    public KycStatus? KycStatus { get; set; }
    public DriverVerificationStatus? DriverVerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public bool OnboardingCompleted { get; set; }
    public bool DriverAccessGranted { get; set; }
}
