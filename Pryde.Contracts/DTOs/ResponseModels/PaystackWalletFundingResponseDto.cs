using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class PaystackWalletFundingResponseDto
{
    public Guid WalletId { get; set; }
    public Guid TransactionId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal NewBalance { get; set; }
    public WalletTransactionStatus Status { get; set; }
}
