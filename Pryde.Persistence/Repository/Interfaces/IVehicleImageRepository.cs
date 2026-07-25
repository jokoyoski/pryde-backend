using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IVehicleImageRepository
{
    Task<VehicleImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleImage>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<VehicleImage> CreateAsync(VehicleImage image, CancellationToken cancellationToken = default);
    void Update(VehicleImage image);
    void Delete(VehicleImage image);
}
