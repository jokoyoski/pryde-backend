namespace Pryde.Services.Settings;
public class PricingSettings
{
    public const string SectionName = "PricingSettings";
    public decimal BaseFare { get; set; }
    public decimal PerKmRate { get; set; }
    public decimal PerMinuteRate { get; set; }
    public decimal MinimumFare { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal PlatformSharePercent { get; set; }
    public double PickupRadiusKm { get; set; }

}