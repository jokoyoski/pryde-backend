using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class RouteMatchingService : IRouteMatchingService
{
    public bool IsPassengerOnRoute(
        double driverOriginLat, double driverOriginLng,
        double driverDestLat, double driverDestLng,
        string? routePolyline,
        double passengerLat, double passengerLng,
        double passengerDestLat, double passengerDestLng,
        double radiusKm)
    {
        if (TryDecodePolyline(routePolyline, out var route))
        {
            var pickup = FindClosestPosition(
                route,
                passengerLat,
                passengerLng);
            if (pickup.DistanceKm > radiusKm) return false;

            var destination = FindClosestPosition(
                route,
                passengerDestLat,
                passengerDestLng);

            return destination.DistanceKm <= radiusKm &&
                   destination.ProgressKm > pickup.ProgressKm;
        }

        return IsPassengerOnStraightLine(
            driverOriginLat, driverOriginLng,
            driverDestLat, driverDestLng,
            passengerLat, passengerLng,
            passengerDestLat, passengerDestLng,
            radiusKm);
    }

    private static bool IsPassengerOnStraightLine(
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

    private static bool TryDecodePolyline(
        string? encoded,
        out List<RoutePoint> points)
    {
        points = [];
        if (string.IsNullOrWhiteSpace(encoded)) return false;

        long latitude = 0;
        long longitude = 0;
        var index = 0;

        while (index < encoded.Length)
        {
            if (!TryDecodeValue(encoded, ref index, out var latitudeDelta) ||
                !TryDecodeValue(encoded, ref index, out var longitudeDelta))
            {
                points.Clear();
                return false;
            }

            latitude += latitudeDelta;
            longitude += longitudeDelta;
            var decodedLatitude = latitude / 1e5;
            var decodedLongitude = longitude / 1e5;

            if (decodedLatitude is < -90 or > 90 ||
                decodedLongitude is < -180 or > 180)
            {
                points.Clear();
                return false;
            }

            points.Add(new RoutePoint(decodedLatitude, decodedLongitude));
        }

        if (points.Count >= 2) return true;

        points.Clear();
        return false;
    }

    private static bool TryDecodeValue(
        string encoded,
        ref int index,
        out long value)
    {
        value = 0;
        var shift = 0;
        long result = 0;

        while (index < encoded.Length && shift <= 30)
        {
            var character = encoded[index++];
            if (character is < '?' or > '~') return false;

            var chunk = character - 63;
            result |= (long)(chunk & 0x1f) << shift;
            if ((chunk & 0x20) == 0)
            {
                value = (result & 1) != 0
                    ? ~(result >> 1)
                    : result >> 1;
                return true;
            }

            shift += 5;
        }

        return false;
    }

    private static RoutePosition FindClosestPosition(
        IReadOnlyList<RoutePoint> route,
        double latitude,
        double longitude)
    {
        var closestDistance = double.MaxValue;
        var closestProgress = 0d;
        var completedDistance = 0d;

        for (var index = 0; index < route.Count - 1; index++)
        {
            var start = route[index];
            var end = route[index + 1];
            var fraction = SegmentProjectionFraction(
                start,
                end,
                latitude,
                longitude);
            var closestLatitude = start.Latitude +
                fraction * (end.Latitude - start.Latitude);
            var closestLongitude = start.Longitude +
                fraction * (end.Longitude - start.Longitude);
            var distance = HaversineKm(
                latitude,
                longitude,
                closestLatitude,
                closestLongitude);
            var segmentLength = HaversineKm(
                start.Latitude,
                start.Longitude,
                end.Latitude,
                end.Longitude);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestProgress = completedDistance +
                    fraction * segmentLength;
            }

            completedDistance += segmentLength;
        }

        return new RoutePosition(closestDistance, closestProgress);
    }

    private static double SegmentProjectionFraction(
        RoutePoint start,
        RoutePoint end,
        double latitude,
        double longitude)
    {
        var referenceLatitude = ToRadians(
            (start.Latitude + end.Latitude + latitude) / 3);
        var longitudeScale = Math.Cos(referenceLatitude);
        var dx = (end.Longitude - start.Longitude) * longitudeScale;
        var dy = end.Latitude - start.Latitude;
        var pointX = (longitude - start.Longitude) * longitudeScale;
        var pointY = latitude - start.Latitude;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return 0;

        return Math.Clamp(
            (pointX * dx + pointY * dy) / lengthSquared,
            0,
            1);
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

    private readonly record struct RoutePoint(double Latitude, double Longitude);
    private readonly record struct RoutePosition(double DistanceKm, double ProgressKm);
}
