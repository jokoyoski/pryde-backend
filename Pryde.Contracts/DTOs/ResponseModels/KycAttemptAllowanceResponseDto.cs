namespace Pryde.Contracts.ResponseModels;

public sealed class KycAttemptAllowanceResponseDto
{
    public int Limit { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
    public bool CanAttempt { get; set; }
    public DateTime ResetsAt { get; set; }
    public string Description { get; set; } = string.Empty;
}
