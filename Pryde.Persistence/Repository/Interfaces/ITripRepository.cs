using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdWithVehicleForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<int> CountCompletedByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
    Task<DriverDashboardTripSummaryData?> GetNextUpcomingByDriverIdAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverDashboardTripSummaryData>> GetLatestCompletedByDriverIdAsync(
        Guid driverId,
        int count,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow,
        DateTime? departureDate,
        bool? requiresLuggage,
        int requiredSeats,
        double? pickupLatitude,
        double? pickupLongitude,
        double? pickupRadiusKm,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetExpiredConfirmationTripIdsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<Trip> CreateAsync(Trip trip, CancellationToken cancellationToken = default);
    void Update(Trip trip);
    void Delete(Trip trip);
}

public class DriverDashboardTripSummaryData
{
    public Guid TripId { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public TripStatus Status { get; set; }
    public decimal SeatPrice { get; set; }
    public int AvailableSeats { get; set; }
    public string VehicleLicensePlateNumber { get; set; } = string.Empty;
    public string? VehicleImageUrl { get; set; }
}
