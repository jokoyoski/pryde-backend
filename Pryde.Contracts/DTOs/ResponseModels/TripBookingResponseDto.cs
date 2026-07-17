using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class TripBookingResponseDto
{
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public Guid PassengerId { get; set; }
    public string? PassengerName { get; set; }
    public string TripOrigin { get; set; } = string.Empty;
    public string TripDestination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public decimal SeatPrice { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
