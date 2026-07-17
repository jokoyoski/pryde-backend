using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripBookingRepository
{
    Task<TripBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TripBooking?> GetByIdWithTripAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetPendingByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetApprovedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveBookingAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripBooking> CreateAsync(TripBooking booking, CancellationToken cancellationToken = default);
    void Update(TripBooking booking);
}
