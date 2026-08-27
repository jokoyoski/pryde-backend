using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public class CreateRecurringTripRequestDto
{
    public Guid VehicleId { get; set; }
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
    public int? BookingWindowMinutes { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public RecurringDays DaysOfWeek { get; set; }
    public TimeOnly DepartureTime { get; set; }
}

public class UpdateRecurringTripRequestDto : CreateRecurringTripRequestDto
{
}

public class AdminRecurringTripsRequestDto : PaginationRequestDto
{
    public Guid? DriverId { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsCancelled { get; set; }
}

public class SavedRecurringTripsRequestDto : PaginationRequestDto
{
}
