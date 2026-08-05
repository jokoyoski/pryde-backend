using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class TripSubscriptionRepository(PrydeDbContext context) : ITripSubscriptionRepository
{
    public async Task<TripSubscription?> GetByRecurringTripAndPassengerAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default)
    {
        return await context.TripSubscriptions
            .Include(s => s.RecurringTrip)
                .ThenInclude(r => r.Vehicle)
            .FirstOrDefaultAsync(s => s.RecurringTripId == recurringTripId && s.PassengerId == passengerId, cancellationToken);
    }

    public async Task<IReadOnlyList<TripSubscription>> GetByPassengerIdAsync(
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        return await context.TripSubscriptions
            .AsNoTracking()
            .Include(s => s.RecurringTrip)
                .ThenInclude(r => r.Vehicle)
            .Where(s => s.PassengerId == passengerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(
        Guid recurringTripId,
        CancellationToken cancellationToken = default)
    {
        return context.TripSubscriptions.CountAsync(
            s => s.RecurringTripId == recurringTripId && s.IsActive,
            cancellationToken);
    }

    public async Task<TripSubscription> CreateAsync(TripSubscription subscription, CancellationToken cancellationToken = default)
    {
        await context.TripSubscriptions.AddAsync(subscription, cancellationToken);
        return subscription;
    }

    public void Update(TripSubscription subscription) => context.TripSubscriptions.Update(subscription);
}
