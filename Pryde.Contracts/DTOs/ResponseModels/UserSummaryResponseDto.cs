namespace Pryde.Contracts.ResponseModels;

public class UserSummaryResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneNumberVerified { get; set; }
    public string KycStatus { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}