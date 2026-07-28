namespace Pryde.Contracts.RequestModels;

public class AdminFundWalletRequest
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
