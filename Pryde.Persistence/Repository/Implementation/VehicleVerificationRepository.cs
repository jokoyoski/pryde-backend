using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;
namespace Pryde.Persistence.Repository.Implementations;
public class VehicleRepository(PrydeDbContext context) : IVehicleRepository
{
    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
    public async Task<Vehicle?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles
            .AsNoTracking()
            .Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
    public async Task<IReadOnlyList<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .ToListAsync(cancellationToken);
    }
    public async Task<Vehicle?> GetByLicensePlateAsync(string licensePlateNumber, CancellationToken cancellationToken = default)
    {
        licensePlateNumber = licensePlateNumber.Trim().ToUpper();
        return await context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.LicensePlateNumber.ToUpper() == licensePlateNumber, cancellationToken);
    }
    public async Task<bool> ExistsAsync(string licensePlateNumber, CancellationToken cancellationToken = default)
    {
        licensePlateNumber = licensePlateNumber.Trim().ToUpper();
        return await context.Vehicles
            .AnyAsync(v => v.LicensePlateNumber.ToUpper() == licensePlateNumber, cancellationToken);
    }
    public async Task<Vehicle> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await context.Vehicles.AddAsync(vehicle, cancellationToken);
        return vehicle;
    }
    public void Update(Vehicle vehicle)
    {
        context.Vehicles.Update(vehicle);
    }
    public void Delete(Vehicle vehicle)
    {
        context.Vehicles.Remove(vehicle);
    }
}