using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class Escrow : BaseEntity
{
    public Guid BookingId { get; set; }
    public TripBooking Booking { get; set; } = null!;
    public Guid PassengerId { get; set; }
    public Guid DriverId { get; set; }
    public decimal Amount { get; set; }
    public decimal DriverAmount { get; set; }
    public decimal PlatformAmount { get; set; }
    public string Currency { get; set; } = "NGN";
    public EscrowStatus Status { get; set; } = EscrowStatus.Held;
    public DateTime HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
