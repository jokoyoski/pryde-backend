namespace Pryde.Services.Trips.Interface;

public interface IRouteMatchingService
{
    bool IsPassengerOnRoute(
        double driverOriginLat, double driverOriginLng,
        double driverDestLat, double driverDestLng,
        double passengerLat, double passengerLng,
        double passengerDestLat, double passengerDestLng,
        double radiusKm);
}