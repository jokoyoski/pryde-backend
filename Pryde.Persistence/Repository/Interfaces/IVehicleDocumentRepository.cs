using Pryde.Domain.Entities;
namespace Pryde.Persistence.Repository.Interfaces;
public interface IVehicleDocumentRepository
{
    Task<VehicleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleDocument>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleDocument>> GetExpiringBeforeAsync(DateTime threshold, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VehicleDocument> CreateAsync(VehicleDocument vehicleDocument, CancellationToken cancellationToken = default);
    void Update(VehicleDocument vehicleDocument);
    void Delete(VehicleDocument vehicleDocument);
}