using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class RegisterResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public List<RoleType> Roles { get; set; } = [];
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}