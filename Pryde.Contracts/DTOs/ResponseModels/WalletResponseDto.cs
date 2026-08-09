namespace Pryde.Contracts.ResponseModels;

public class WalletResponseDto
{
    public Guid Id { get; set; }
    public decimal Balance { get; set; }
    public decimal WithdrawableBalance { get; set; }
    public decimal EscrowBalance { get; set; }
}
