using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class TripSummaryResponseDto : WorkflowResponseDto
{
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public string VehicleLicensePlateNumber { get; set; } = string.Empty;
    public int VehicleCapacity { get; set; }
    public List<string> VehicleImageUrls { get; set; } = [];
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLatitude { get; set; }
    public double OriginLongitude { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public string? RoutePolyline { get; set; }
    public DateTime DepartureTime { get; set; }
    public int AvailableSeats { get; set; }
    public bool AllowLuggage { get; set; }
    public double DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal TripFare { get; set; }
    public decimal SeatPrice { get; set; }
    public decimal ServiceChargePercentage { get; set; }
    public decimal PassengerServiceCharge { get; set; }
    public decimal PassengerTotal { get; set; }
    public int BookingWindowMinutes { get; set; }
    public TripStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
