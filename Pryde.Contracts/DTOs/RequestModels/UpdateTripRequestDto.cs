namespace Pryde.Contracts.RequestModels;

public class UpdateTripRequestDto
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
    public int? BookingWindowMinutes { get; set; }
    public string? RoutePolyline { get; set; }
}
