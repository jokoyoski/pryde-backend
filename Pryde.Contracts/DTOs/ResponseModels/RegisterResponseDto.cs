using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class RegisterResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public bool EmailVerificationRequired { get; set; }
}
