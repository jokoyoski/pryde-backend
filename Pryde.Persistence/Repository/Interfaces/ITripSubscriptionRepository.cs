using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripSubscriptionRepository
{
    Task<TripSubscription?> GetByRecurringTripAndPassengerAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripSubscription?> GetByRecurringTripAndPassengerForUpdateAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripSubscription>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetActivePassengerIdsAsync(Guid recurringTripId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(Guid recurringTripId, CancellationToken cancellationToken = default);
    Task<TripSubscription> CreateAsync(TripSubscription subscription, CancellationToken cancellationToken = default);
    void Update(TripSubscription subscription);
}
