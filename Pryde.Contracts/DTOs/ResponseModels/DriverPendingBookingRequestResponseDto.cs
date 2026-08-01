namespace Pryde.Contracts.ResponseModels;

public class DriverPendingBookingRequestResponseDto
{
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public Guid PassengerId { get; set; }
    public string? PassengerName { get; set; }
    public string? PassengerProfileImageUrl { get; set; }
    public int RequestedSeats { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime TripDepartureTime { get; set; }
    public DateTime RequestedAt { get; set; }
}
