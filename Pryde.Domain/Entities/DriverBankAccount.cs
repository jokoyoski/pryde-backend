using Pryde.Domain.Common;

namespace Pryde.Domain.Entities;

public class DriverBankAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string RecipientCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
