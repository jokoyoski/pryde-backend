namespace Pryde.Services.Settings;
public class PricingSettings
{
    public const string SectionName = "PricingSettings";
    public const string PlatformShareValidationError =
        "PricingSettings:PlatformSharePercent must be between 0 and 100.";
    public decimal BaseFare { get; set; }
    public decimal PerKmRate { get; set; }
    public decimal PerMinuteRate { get; set; }
    public decimal MinimumFare { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal LagosMaintenanceFee { get; set; } = 30m;
    public decimal PlatformSharePercent { get; set; }
    public double PickupRadiusKm { get; set; }

    public static bool HasValidPlatformShare(PricingSettings settings) =>
        settings.PlatformSharePercent is >= 0m and <= 100m;

    public decimal CalculatePassengerServiceCharge(
        decimal seatPrice,
        decimal serviceChargePercentage) =>
        Math.Round(seatPrice * serviceChargePercentage / 100m, 2) +
        LagosMaintenanceFee;
}
