using Pryde.Domain.Common;
using Pryde.Domain.Enums;
using System.Net.NetworkInformation;

namespace Pryde.Domain.Entities
{
    public class Trip : BaseEntity
    {
        public Guid DriverId { get; set; }
        public User Driver { get; set; } = null!;

        public Guid VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!;

        public double OriginLatitude { get; set; }
        public double OriginLongitude { get; set; }
        public double DestinationLatitude { get; set; }
        public double DestinationLongitude { get; set; }
        public string DestinationAddress { get; set; } = string.Empty;

        public string? RoutePolyline { get; set; }
        public double DistanceKm { get; set; }
        public int EstimatedDurationMinutes { get; set; }

        public DateTime DepartureTime { get; set; }
        public int AvailableSeats { get; set; }
        public bool AllowLuggage { get; set; }

        public decimal TripFare { get; set; }
        public decimal SeatPrice { get; set; }
        public decimal ServiceChargePercentage { get; set; }
        public int BookingWindowHours { get; set; } = 5;
        public string OriginAddress { get; set; } = string.Empty;

        public TripStatus Status { get; set; } = TripStatus.Scheduled;
        public DateTime? DriverEndedAt { get; set; }
        public DateTime? ConfirmationDeadline { get; set; }
        public DateTime? AutoCompletedAt { get; set; }
        public bool WasAutoCompleted { get; set; }

        public Guid? RecurringTripId { get; set; }
        public RecurringTrip? RecurringTrip { get; set; }

        public ICollection<TripBooking> Bookings { get; set; } = new List<TripBooking>();
    }
}
