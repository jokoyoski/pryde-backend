namespace Pryde.Contracts.RequestModels;

public class ResolveBankAccountRequestDto
{
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class CreateDriverBankAccountRequestDto
{
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}
