namespace Pryde.Services.Service.Interface;

public class FareBreakdown
{
    public decimal TotalTripCost { get; set; }
    public decimal SeatPrice { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal ServiceChargePercentage { get; set; }
    public decimal PassengerTotal { get; set; }
}

public interface IFareCalculator
{
    FareBreakdown Calculate(double distanceKm, int durationMinutes, int vehicleCapacity);
}
