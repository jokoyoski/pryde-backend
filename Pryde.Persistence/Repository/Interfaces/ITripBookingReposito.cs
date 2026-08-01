using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripBookingRepository
{
    Task<TripBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TripBooking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TripBooking?> GetByIdWithTripAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetPendingByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<DriverPendingBookingRequestData> Items,
        int TotalCount)> GetPendingByDriverIdAsync(
        Guid driverId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetApprovedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default);
    Task<int> CountPendingByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetExpiredUnpaidApprovedBookingIdsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<bool> HasActiveBookingAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripBooking> CreateAsync(TripBooking booking, CancellationToken cancellationToken = default);
    void Update(TripBooking booking);
}

public class DriverPendingBookingRequestData
{
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public Guid PassengerId { get; set; }
    public string? PassengerName { get; set; }
    public string? PassengerProfileImageUrl { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime TripDepartureTime { get; set; }
    public DateTime RequestedAt { get; set; }
}
