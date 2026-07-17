namespace Pryde.Contracts.ResponseModels;

public class VirtualAccountResponseDto
{
    public Guid Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
