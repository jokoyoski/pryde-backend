namespace Pryde.Contracts.RequestModels;

public class CreateDriverWithdrawalRequestDto
{
    public Guid DriverBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Otp { get; set; } = string.Empty;
}

public class DriverWithdrawalOtpRequestDto
{
    public Guid DriverBankAccountId { get; set; }
    public decimal Amount { get; set; }
}
