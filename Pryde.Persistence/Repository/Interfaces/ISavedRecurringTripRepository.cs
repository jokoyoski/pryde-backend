using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ISavedRecurringTripRepository
{
    Task<SavedRecurringTrip?> GetAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<SavedRecurringTrip?> GetForUpdateAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SavedRecurringTrip> Items, int TotalCount)> GetByPassengerIdAsync(Guid passengerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<SavedRecurringTrip> CreateAsync(SavedRecurringTrip savedRecurringTrip, CancellationToken cancellationToken = default);
    void Delete(SavedRecurringTrip savedRecurringTrip);
}
