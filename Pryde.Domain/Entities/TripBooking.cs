using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class TripBooking : BaseEntity
    {
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public Guid PassengerId { get; set; }
        public User Passenger { get; set; } = null!;

        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public bool PickupConfirmed { get; set; }
        public bool DropoffConfirmed { get; set; }

        public decimal SeatPrice { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public Escrow? Escrow { get; set; }
    }
}
