using Microsoft.Extensions.Options;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public class FareCalculator(IOptions<PricingSettings> pricingSettings) : IFareCalculator
{
    private readonly PricingSettings _settings = pricingSettings.Value;

    public FareBreakdown Calculate(double distanceKm, int durationMinutes, int vehicleCapacity)
    {
        var rawCost = _settings.BaseFare
            + (_settings.PerKmRate * (decimal)distanceKm)
            + (_settings.PerMinuteRate * durationMinutes);

        var totalTripCost = Math.Max(rawCost, _settings.MinimumFare);
        var seatPrice = Math.Round(totalTripCost / vehicleCapacity, 2);
        var serviceCharge = Math.Round(seatPrice * (_settings.ServiceChargePercent / 100m), 2);

        return new FareBreakdown
        {
            TotalTripCost = totalTripCost,
            SeatPrice = seatPrice,
            ServiceCharge = serviceCharge,
            ServiceChargePercentage = _settings.ServiceChargePercent,
            PassengerTotal = seatPrice + serviceCharge
        };
    }
}
