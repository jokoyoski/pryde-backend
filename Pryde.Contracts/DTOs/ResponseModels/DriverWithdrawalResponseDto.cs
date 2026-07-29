using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class DriverWithdrawalResponseDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public WalletTransactionStatus Status { get; set; }
    public string? ProviderReference { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class DriverWithdrawalOtpResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ResendAvailableAt { get; set; }
}
