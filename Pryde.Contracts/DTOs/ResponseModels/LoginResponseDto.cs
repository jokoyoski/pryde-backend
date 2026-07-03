namespace Pryde.Contracts.ResponseModels;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}
