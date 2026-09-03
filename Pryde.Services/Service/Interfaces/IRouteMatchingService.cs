namespace Pryde.Services.Service.Interface;

public interface IRouteMatchingService
{
    bool IsPassengerOnRoute(
        double driverOriginLat, double driverOriginLng,
        double driverDestLat, double driverDestLng,
        string? routePolyline,
        double passengerLat, double passengerLng,
        double passengerDestLat, double passengerDestLng,
        double radiusKm);
}
