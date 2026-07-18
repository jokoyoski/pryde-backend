using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;
namespace Pryde.Services.Service.Interface;
public interface IVehicleDocumentService
{
    Task<VehicleDocumentResponseDto> UploadAsync(Guid vehicleId, Guid requestingUserId, VehicleDocumentType documentType, DateTime expiryDate, string documentUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleDocumentResponseDto>> GetByVehicleIdAsync(Guid vehicleId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleDocumentResponseDto>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken = default);
    Task<VehicleDocumentResponseDto> GetForAdminAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<VehicleDocumentResponseDto> ApproveAsync(Guid documentId, Guid reviewedBy, CancellationToken cancellationToken = default);
    Task<VehicleDocumentResponseDto> RejectAsync(Guid documentId, Guid reviewedBy, string reason, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentId, Guid requestingUserId, CancellationToken cancellationToken = default);
}
