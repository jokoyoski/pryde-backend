using Pryde.Domain.Enums;
namespace Pryde.Contracts.ResponseModels;
public class VehicleDocumentResponseDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public VehicleDocumentType DocumentType { get; set; }
    public string DocumentUrl { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
} 