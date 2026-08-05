using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class RecurringTripRepository(PrydeDbContext context) : IRecurringTripRepository
{
    public async Task<RecurringTrip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ScheduleQuery().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RecurringTrip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.RecurringTrips
            .Include(r => r.Vehicle)
            .Include(r => r.Subscriptions)
                .ThenInclude(s => s.Passenger)
            .Include(r => r.Trips)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTrip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await ScheduleQuery()
            .Where(r => r.DriverId == driverId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTrip>> GetActiveForGenerationAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await context.RecurringTrips
            .AsNoTracking()
            .Where(r => r.IsActive && r.CancelledAt == null &&
                r.StartDate <= to && (!r.EndDate.HasValue || r.EndDate.Value >= from))
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<RecurringTrip> Items, int TotalCount)> GetAllAsync(
        Guid? driverId,
        bool? isActive,
        bool? isCancelled,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ScheduleQuery();
        if (driverId.HasValue)
            query = query.Where(r => r.DriverId == driverId.Value);
        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);
        if (isCancelled.HasValue)
            query = query.Where(r => r.CancelledAt.HasValue == isCancelled.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<RecurringTrip> CreateAsync(RecurringTrip recurringTrip, CancellationToken cancellationToken = default)
    {
        await context.RecurringTrips.AddAsync(recurringTrip, cancellationToken);
        return recurringTrip;
    }

    public void Update(RecurringTrip recurringTrip) => context.RecurringTrips.Update(recurringTrip);

    private IQueryable<RecurringTrip> ScheduleQuery() => context.RecurringTrips
        .AsNoTracking()
        .Include(r => r.Driver)
            .ThenInclude(d => d.Profile)
        .Include(r => r.Vehicle)
        .Include(r => r.Subscriptions)
        .Include(r => r.Trips);
}
