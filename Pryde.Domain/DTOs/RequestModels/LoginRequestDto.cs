namespace Pryde.Domain.DTOs.RequestModels;

public class LoginRequestDto
{
    public string EmailOrPhone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
