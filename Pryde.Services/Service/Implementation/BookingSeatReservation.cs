using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Services.Service.Implementation;

internal static class BookingSeatReservation
{
    public static bool CancelApprovedBooking(TripBooking booking)
    {
        if (booking.Status != BookingStatus.Approved)
        {
            return false;
        }

        booking.Trip.AvailableSeats = Math.Min(
            booking.Trip.Vehicle.Capacity,
            booking.Trip.AvailableSeats + 1);
        booking.Status = BookingStatus.Cancelled;
        return true;
    }
}
