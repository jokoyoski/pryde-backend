namespace Pryde.Contracts.RequestModels;

public class CreateTripRequestDto
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
    public DateTime DepartureTime { get; set; }
    public int AvailableSeats { get; set; }
    public bool AllowLuggage { get; set; }
    public int BookingWindowHours { get; set; } = 5;
    public string? RoutePolyline { get; set; }
}
