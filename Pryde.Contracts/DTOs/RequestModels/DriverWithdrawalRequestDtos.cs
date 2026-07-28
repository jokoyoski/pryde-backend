namespace Pryde.Contracts.RequestModels;

public class CreateDriverWithdrawalRequestDto
{
    public Guid DriverBankAccountId { get; set; }
    public decimal Amount { get; set; }
}
