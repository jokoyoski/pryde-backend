using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IRecurringTripRepository
{
    Task<RecurringTrip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringTrip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTrip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTrip>> GetActiveForGenerationAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<RecurringTrip> Items, int TotalCount)> GetAllAsync(Guid? driverId, bool? isActive, bool? isCancelled, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<RecurringTrip> CreateAsync(RecurringTrip recurringTrip, CancellationToken cancellationToken = default);
    void Update(RecurringTrip recurringTrip);
}
