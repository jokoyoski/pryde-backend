using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;
namespace Pryde.Persistence.Repository.Implementations;
public class VehicleDocumentRepository(PrydeDbContext context) : IVehicleDocumentRepository
{
    public async Task<VehicleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.VehicleDocuments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
    public async Task<IReadOnlyList<VehicleDocument>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await context.VehicleDocuments
            .AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<VehicleDocument>> GetExpiringBeforeAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        return await context.VehicleDocuments
            .AsNoTracking()
            .Include(d => d.Vehicle)
                .ThenInclude(v => v.User)
            .Where(d => d.ExpiryDate <= threshold)
            .ToListAsync(cancellationToken);
    }
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.VehicleDocuments
            .AnyAsync(d => d.Id == id, cancellationToken);
    }
    public async Task<VehicleDocument> CreateAsync(VehicleDocument vehicleDocument, CancellationToken cancellationToken = default)
    {
        await context.VehicleDocuments.AddAsync(vehicleDocument, cancellationToken);
        return vehicleDocument;
    }
    public void Update(VehicleDocument vehicleDocument)
    {
        context.VehicleDocuments.Update(vehicleDocument);
    }
    public void Delete(VehicleDocument vehicleDocument)
    {
        context.VehicleDocuments.Remove(vehicleDocument);
    }
}