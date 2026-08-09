using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class RecurringTripResponseDto
{
    public Guid RecurringTripId { get; set; }
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string VehicleLicensePlateNumber { get; set; } = string.Empty;
    public double OriginLatitude { get; set; }
    public double OriginLongitude { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string? RoutePolyline { get; set; }
    public int AvailableSeats { get; set; }
    public bool AllowLuggage { get; set; }
    public int BookingWindowMinutes { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public RecurringDays DaysOfWeek { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int ActiveSubscriptionCount { get; set; }
    public IReadOnlyList<RecurringTripOccurrenceResponseDto> GeneratedTrips { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class RecurringTripOccurrenceResponseDto
{
    public Guid TripId { get; set; }
    public DateTime DepartureTime { get; set; }
    public TripStatus Status { get; set; }
    public int AvailableSeats { get; set; }
}

public class TripSubscriptionResponseDto
{
    public Guid SubscriptionId { get; set; }
    public Guid RecurringTripId { get; set; }
    public Guid PassengerId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public RecurringDays DaysOfWeek { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public DateTime CreatedAt { get; set; }
}
