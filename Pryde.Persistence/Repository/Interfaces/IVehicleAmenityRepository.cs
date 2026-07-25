using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IVehicleAmenityRepository
{
    Task<IReadOnlyList<VehicleAmenity>> GetByVehicleIdAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    Task<VehicleAmenity> CreateAsync(
        VehicleAmenity amenity,
        CancellationToken cancellationToken = default);

    void Delete(VehicleAmenity amenity);
}
