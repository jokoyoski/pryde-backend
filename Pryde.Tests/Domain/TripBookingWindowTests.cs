using Pryde.Domain.Common;
using Pryde.Domain.Entities;

namespace Pryde.Tests.Domain;

public class TripBookingWindowTests
{
    [Fact]
    public void GetClosesAtUtcSubtractsMinutesFromUtcDeparture()
    {
        var departureUtc = new DateTime(
            2026, 8, 9, 12, 30, 0, DateTimeKind.Utc);

        var bookingClosesAt = TripBookingWindow.GetClosesAtUtc(
            departureUtc,
            15);

        Assert.Equal(
            new DateTime(2026, 8, 9, 12, 15, 0, DateTimeKind.Utc),
            bookingClosesAt);
        Assert.Equal(DateTimeKind.Utc, bookingClosesAt.Kind);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(14, false)]
    public void IsOpenAtUtcUsesStrictCutoffBoundary(
        int departureMinutes,
        bool expected)
    {
        var utcNow = new DateTime(
            2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var trip = new Trip
        {
            DepartureTime = utcNow.AddMinutes(departureMinutes),
            BookingWindowMinutes = 15
        };

        var result = TripBookingWindow.IsOpenAtUtc(utcNow)
            .Compile()(trip);

        Assert.Equal(expected, result);
    }
}
