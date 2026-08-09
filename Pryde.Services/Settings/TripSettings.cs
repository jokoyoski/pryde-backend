using Pryde.Domain.Common;

namespace Pryde.Services.Settings;

public sealed class TripSettings
{
    public const string SectionName = "Trips";
    public int DefaultBookingWindowMinutes { get; set; } =
        TripBookingWindow.DefaultMinutes;
}
