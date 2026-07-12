namespace Pryde.Contracts.ResponseModels;

public class FundVirtualAccountResponseDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal UpdatedBalance { get; set; }
    public Guid TransactionId { get; set; }
    public string Reference { get; set; } = string.Empty;
}
