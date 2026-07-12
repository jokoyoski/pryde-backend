namespace Pryde.Contracts.RequestModels;

public class FundVirtualAccountRequestDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
