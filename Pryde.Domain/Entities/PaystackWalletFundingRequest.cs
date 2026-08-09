using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class PaystackWalletFundingRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Reference { get; set; } = string.Empty;
    public long ExpectedAmountKobo { get; set; }
    public string Currency { get; set; } = "NGN";
    public string CustomerEmail { get; set; } = string.Empty;
    public PaystackWalletFundingRequestStatus Status { get; set; } = PaystackWalletFundingRequestStatus.Pending;
    public long? PaystackTransactionId { get; set; }
    public DateTime? CompletedAt { get; set; }
}
