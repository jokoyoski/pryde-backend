using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class VehicleImageRepository(PrydeDbContext context) : IVehicleImageRepository
{
    public async Task<VehicleImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.VehicleImages.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleImage>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await context.VehicleImages
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<VehicleImage> CreateAsync(VehicleImage image, CancellationToken cancellationToken = default)
    {
        await context.VehicleImages.AddAsync(image, cancellationToken);
        return image;
    }

    public void Update(VehicleImage image) => context.VehicleImages.Update(image);

    public void Delete(VehicleImage image) => context.VehicleImages.Remove(image);
}
