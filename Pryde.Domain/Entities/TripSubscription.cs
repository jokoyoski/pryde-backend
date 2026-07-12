using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class TripSubscription : BaseEntity
    {
        public Guid RecurringTripId { get; set; }
        public RecurringTrip RecurringTrip { get; set; } = null!;

        public Guid PassengerId { get; set; }
        public User Passenger { get; set; } = null!;

        //public bool IsActive { get; set; } = true;
    }
}