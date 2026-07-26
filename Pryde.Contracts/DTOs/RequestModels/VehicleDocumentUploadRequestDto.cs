using Microsoft.AspNetCore.Http;
using Pryde.Domain.Enums;
namespace Pryde.Contracts.RequestModels;
public class VehicleDocumentUploadRequestDto
{
    public VehicleDocumentType DocumentType { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public IFormFile? Document { get; init; }
}
