namespace Pryde.Domain.DTOs.ResponseModels;

public class AuthResponseDto
{
    public Guid UserId { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? AccessToken { get; set; }
}
