using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class DriverDashboardTripSummaryResponseDto
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
