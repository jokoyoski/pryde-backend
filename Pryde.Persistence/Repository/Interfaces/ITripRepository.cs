using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow,
        DateTime? departureDate,
        bool? requiresLuggage,
        int requiredSeats,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Trip> CreateAsync(Trip trip, CancellationToken cancellationToken = default);
    void Update(Trip trip);
    void Delete(Trip trip);
}
