using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

// Placeholder — approximates the driver's path as a straight line between
// origin and destination. Swap for real polyline-based corridor checking
// once Google Routes is wired in; same interface, callers don't change.
public class RouteMatchingService : IRouteMatchingService
{
    public bool IsPassengerOnRoute(
        double driverOriginLat, double driverOriginLng,
        double driverDestLat, double driverDestLng,
        double passengerLat, double passengerLng,
        double passengerDestLat, double passengerDestLng,
        double radiusKm)
    {
        var pickupDistance = DistanceFromLineKm(
            driverOriginLat, driverOriginLng, driverDestLat, driverDestLng,
            passengerLat, passengerLng);

        if (pickupDistance > radiusKm) return false;

        var pickupProgress = ProjectionFraction(
            driverOriginLat, driverOriginLng, driverDestLat, driverDestLng,
            passengerLat, passengerLng);

        var dropoffProgress = ProjectionFraction(
            driverOriginLat, driverOriginLng, driverDestLat, driverDestLng,
            passengerDestLat, passengerDestLng);

        return dropoffProgress > pickupProgress;
    }

    private static double ProjectionFraction(
        double aLat, double aLng, double bLat, double bLng, double pLat, double pLng)
    {
        var dx = bLng - aLng;
        var dy = bLat - aLat;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return 0;

        return ((pLng - aLng) * dx + (pLat - aLat) * dy) / lengthSquared;
    }

    private static double DistanceFromLineKm(
        double aLat, double aLng, double bLat, double bLng, double pLat, double pLng)
    {
        var t = ProjectionFraction(aLat, aLng, bLat, bLng, pLat, pLng);
        t = Math.Clamp(t, 0, 1);

        var closestLat = aLat + t * (bLat - aLat);
        var closestLng = aLng + t * (bLng - aLng);

        return HaversineKm(pLat, pLng, closestLat, closestLng);
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
