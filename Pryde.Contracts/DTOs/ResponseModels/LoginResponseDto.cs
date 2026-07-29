using System.Text.Json.Serialization;
using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class LoginResponseDto : WorkflowResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public OnboardingStatusResponseDto Onboarding { get; set; } = new();

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OnboardingStage? WorkflowStatus { get; set; }
}
