namespace Pryde.Contracts.RequestModels;

public class EmailVerificationResendRequestDto
{
    public string Email { get; set; } = string.Empty;
}

public class EmailVerificationVerifyRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
