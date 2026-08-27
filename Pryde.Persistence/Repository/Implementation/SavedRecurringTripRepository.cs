using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class SavedRecurringTripRepository(PrydeDbContext context) : ISavedRecurringTripRepository
{
    public Task<SavedRecurringTrip?> GetAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(item => item.RecurringTripId == recurringTripId && item.PassengerId == passengerId, cancellationToken);

    public Task<SavedRecurringTrip?> GetForUpdateAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default) =>
        context.SavedRecurringTrips.FirstOrDefaultAsync(item => item.RecurringTripId == recurringTripId && item.PassengerId == passengerId, cancellationToken);

    public async Task<(IReadOnlyList<SavedRecurringTrip> Items, int TotalCount)> GetByPassengerIdAsync(Guid passengerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Query().Where(item => item.PassengerId == passengerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<SavedRecurringTrip> CreateAsync(SavedRecurringTrip savedRecurringTrip, CancellationToken cancellationToken = default)
    {
        await context.SavedRecurringTrips.AddAsync(savedRecurringTrip, cancellationToken);
        return savedRecurringTrip;
    }

    public void Delete(SavedRecurringTrip savedRecurringTrip) => context.SavedRecurringTrips.Remove(savedRecurringTrip);

    private IQueryable<SavedRecurringTrip> Query() => context.SavedRecurringTrips
        .AsNoTracking()
        .Include(item => item.RecurringTrip).ThenInclude(schedule => schedule.Driver).ThenInclude(driver => driver.Profile)
        .Include(item => item.RecurringTrip).ThenInclude(schedule => schedule.Vehicle)
        .Include(item => item.RecurringTrip).ThenInclude(schedule => schedule.Subscriptions);
}
