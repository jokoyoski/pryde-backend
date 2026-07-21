namespace Pryde.Contracts.ResponseModels;

public class EmailVerificationResendResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime ResendAvailableAt { get; set; }
    public int ResendCooldownSeconds { get; set; }
}

public class VerificationStatusResponseDto
{
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneNumberVerified { get; set; }
    public string ActiveVerificationChannel { get; set; } = "Email";
    public bool EmailVerificationRequired { get; set; }
    public bool PhoneVerificationSuspended { get; set; } = true;
    public DateTime? ResendAvailableAt { get; set; }
    public int ResendCooldownSeconds { get; set; }
    public DateTime? VerificationCodeExpiresAt { get; set; }
}
