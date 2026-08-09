namespace Pryde.Contracts.ResponseModels;

public class WalletFundingRequestResponseDto
{
    public string Reference { get; set; } = string.Empty;
    public long AmountKobo { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
