using System.Text.Json.Serialization;
using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class EmailVerificationResendResponseDto : WorkflowResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime ResendAvailableAt { get; set; }
    public int ResendCooldownSeconds { get; set; }
    public WorkflowOperationStatus Status { get; set; }
}

public class VerificationStatusResponseDto : WorkflowResponseDto
{
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneNumberVerified { get; set; }
    public string ActiveVerificationChannel { get; set; } = "Email";
    public bool EmailVerificationRequired { get; set; }
    public bool PhoneVerificationSuspended { get; set; } = true;
    public DateTime? ResendAvailableAt { get; set; }
    public int ResendCooldownSeconds { get; set; }
    public DateTime? VerificationCodeExpiresAt { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserStatus? WorkflowStatus { get; set; }
}
