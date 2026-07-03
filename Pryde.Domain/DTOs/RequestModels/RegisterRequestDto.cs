using Pryde.Domain.Enums;

namespace Pryde.Domain.DTOs.RequestModels;

public class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<RoleType> Roles { get; set; } = [];
}
