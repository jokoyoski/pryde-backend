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
            .FirstOrDefaultAsync(s => s.RecurringTripId == recurringTripId && s.PassengerId == passengerId, cancellationToken);
    }

    public async Task<TripSubscription> CreateAsync(TripSubscription subscription, CancellationToken cancellationToken = default)
    {
        await context.TripSubscriptions.AddAsync(subscription, cancellationToken);
        return subscription;
    }

    public void Update(TripSubscription subscription) => context.TripSubscriptions.Update(subscription);
}