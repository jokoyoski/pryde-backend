using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class RecurringTripRepository(PrydeDbContext context) : IRecurringTripRepository
{
    public async Task<RecurringTrip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.RecurringTrips.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTrip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await context.RecurringTrips.AsNoTracking().Where(r => r.DriverId == driverId).ToListAsync(cancellationToken);
    }

    public async Task<RecurringTrip> CreateAsync(RecurringTrip recurringTrip, CancellationToken cancellationToken = default)
    {
        await context.RecurringTrips.AddAsync(recurringTrip, cancellationToken);
        return recurringTrip;
    }

    public void Update(RecurringTrip recurringTrip) => context.RecurringTrips.Update(recurringTrip);
}