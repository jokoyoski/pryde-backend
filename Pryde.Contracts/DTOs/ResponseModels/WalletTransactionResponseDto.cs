using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class WalletTransactionResponseDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public WalletTransactionStatus? Status { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}
