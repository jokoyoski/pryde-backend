namespace Pryde.Contracts.ResponseModels;

public class AdminFundWalletResponseDto
{
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
    public decimal AmountFunded { get; set; }
    public decimal NewBalance { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
}
