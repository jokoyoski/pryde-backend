using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class RecurringTrip : BaseEntity
    {
        public Guid DriverId { get; set; }
        public User Driver { get; set; } = null!;

        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public RecurringDays DaysOfWeek { get; set; } = RecurringDays.None;
        public bool IsActive { get; set; } = true;

        public ICollection<Trip> Trips { get; set; } = new List<Trip>();
        public ICollection<TripSubscription> Subscriptions { get; set; } = new List<TripSubscription>();
    }
}