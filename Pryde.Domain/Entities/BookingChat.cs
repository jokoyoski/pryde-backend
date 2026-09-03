using Pryde.Domain.Common;

namespace Pryde.Domain.Entities;

public class BookingChat : BaseEntity
{
    public Guid BookingId { get; set; }
    public TripBooking Booking { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } =
        new List<ChatMessage>();
}
