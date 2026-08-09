using System.Linq.Expressions;
using Pryde.Domain.Entities;

namespace Pryde.Domain.Common;

public static class TripBookingWindow
{
    public const int DefaultMinutes = 15;

    public static DateTime GetClosesAtUtc(
        DateTime departureUtc,
        int bookingWindowMinutes)
    {
        var normalizedDeparture = departureUtc.Kind == DateTimeKind.Utc
            ? departureUtc
            : departureUtc.ToUniversalTime();
        return normalizedDeparture -
            TimeSpan.FromMinutes(bookingWindowMinutes);
    }

    public static Expression<Func<Trip, bool>> IsOpenAtUtc(
        DateTime utcNow)
    {
        var normalizedNow = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : utcNow.ToUniversalTime();
        return trip => trip.DepartureTime.AddMinutes(
            -trip.BookingWindowMinutes) > normalizedNow;
    }
}
