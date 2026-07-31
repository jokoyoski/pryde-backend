namespace Pryde.Contracts.ResponseModels;

public sealed class DojahVerificationDetailsResponseDto
{
    public string Reference { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? VerificationMode { get; set; }
    public string? VerificationType { get; set; }
    public bool? IdResult { get; set; }
    public bool? LivenessResult { get; set; }
    public bool? GovernmentDataResult { get; set; }
    public string? DocumentType { get; set; }
    public string? FullName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Country { get; set; }
    public string? IssueDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? MaskedDocumentNumber { get; set; }
    public string? FrontDocumentImageUrl { get; set; }
    public string? BackDocumentImageUrl { get; set; }
}
