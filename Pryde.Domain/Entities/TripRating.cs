using Pryde.Domain.Common;

namespace Pryde.Domain.Entities;

public class TripRating : BaseEntity
{
    public Guid BookingId { get; set; }
    public TripBooking Booking { get; set; } = null!;

    public Guid RaterId { get; set; }
    public User Rater { get; set; } = null!;

    public Guid RatedUserId { get; set; }
    public User RatedUser { get; set; } = null!;

    public int Value { get; set; }
    public string? Comment { get; set; }
}
