namespace Pryde.Services.Settings;

public sealed class RecurringTripSettings
{
    public const string SectionName = "RecurringTrips";

    public int GenerationHorizonDays { get; set; } = 14;
    public int GenerationIntervalMinutes { get; set; } = 15;
}
