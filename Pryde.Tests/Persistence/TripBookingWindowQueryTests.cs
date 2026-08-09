using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Common;
using Pryde.Persistence.Context;

namespace Pryde.Tests.Persistence;

public class TripBookingWindowQueryTests
{
    [Fact]
    public void PostgreSqlProviderTranslatesSharedBookingCutoffExpression()
    {
        var options = new DbContextOptionsBuilder<PrydeDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=pryde_test;Username=test;Password=test")
            .Options;
        using var context = new PrydeDbContext(options);
        var utcNow = new DateTime(
            2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        var sql = context.Trips
            .Where(TripBookingWindow.IsOpenAtUtc(utcNow))
            .ToQueryString();

        Assert.Contains("BookingWindowMinutes", sql);
        Assert.Contains("WHERE", sql);
    }
}
