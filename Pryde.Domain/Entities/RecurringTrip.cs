using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class RecurringTrip : BaseEntity
    {
        public Guid DriverId { get; set; }
        public User Driver { get; set; } = null!;

        public Guid? VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public double OriginLatitude { get; set; }
        public double OriginLongitude { get; set; }
        public string OriginAddress { get; set; } = string.Empty;
        public double DestinationLatitude { get; set; }
        public double DestinationLongitude { get; set; }
        public string DestinationAddress { get; set; } = string.Empty;
        public string? RoutePolyline { get; set; }
        public double DistanceKm { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public TimeOnly DepartureTime { get; set; }
        public int AvailableSeats { get; set; }
        public bool AllowLuggage { get; set; }
        public int BookingWindowMinutes { get; set; } =
            TripBookingWindow.DefaultMinutes;

        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public RecurringDays DaysOfWeek { get; set; } = RecurringDays.None;
        public bool IsActive { get; set; } = true;
        public DateTime? CancelledAt { get; set; }

        public ICollection<Trip> Trips { get; set; } = new List<Trip>();
        public ICollection<TripSubscription> Subscriptions { get; set; } = new List<TripSubscription>();
    }
}
