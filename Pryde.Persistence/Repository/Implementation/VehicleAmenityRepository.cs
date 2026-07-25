using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class VehicleAmenityRepository(PrydeDbContext context)
    : IVehicleAmenityRepository
{
    public async Task<IReadOnlyList<VehicleAmenity>> GetByVehicleIdAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        return await context.VehicleAmenities
            .Where(x => x.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<VehicleAmenity> CreateAsync(
        VehicleAmenity amenity,
        CancellationToken cancellationToken = default)
    {
        await context.VehicleAmenities.AddAsync(amenity, cancellationToken);
        return amenity;
    }

    public void Delete(VehicleAmenity amenity) =>
        context.VehicleAmenities.Remove(amenity);
}
