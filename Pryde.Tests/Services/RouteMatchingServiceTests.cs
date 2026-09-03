using Pryde.Services.Service.Implementation;

namespace Pryde.Tests.Services;

public class RouteMatchingServiceTests
{
    private const string CurvedRoute =
        "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    private readonly RouteMatchingService _service = new();

    [Fact]
    public void ValidCurvedRouteMatchesPickupAndDestinationInOrder()
    {
        Assert.True(IsMatch(
            CurvedRoute,
            40.7, -120.95,
            43.252, -126.453,
            1));
    }

    [Fact]
    public void ValidRouteRejectsPickupOffRoute()
    {
        Assert.False(IsMatch(
            CurvedRoute,
            39.6, -119,
            43.252, -126.453,
            1));
    }

    [Fact]
    public void ValidRouteRejectsDestinationOffRoute()
    {
        Assert.False(IsMatch(
            CurvedRoute,
            40.7, -120.95,
            41.5, -120,
            1));
    }

    [Fact]
    public void ValidRouteRejectsReversedDirection()
    {
        Assert.False(IsMatch(
            CurvedRoute,
            43.252, -126.453,
            40.7, -120.95,
            1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc!")]
    [InlineData("_")]
    [InlineData("_p~iF~ps|U")]
    public void InvalidPolylineUsesExistingStraightLineFallback(
        string? routePolyline)
    {
        Assert.True(IsMatch(
            routePolyline,
            0, 0.25,
            10, 0.75,
            0.1,
            0, 0,
            0, 1));
    }

    private bool IsMatch(
        string? routePolyline,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        double radiusKm,
        double driverOriginLatitude = 38.5,
        double driverOriginLongitude = -120.2,
        double driverDestinationLatitude = 43.252,
        double driverDestinationLongitude = -126.453)
    {
        return _service.IsPassengerOnRoute(
            driverOriginLatitude,
            driverOriginLongitude,
            driverDestinationLatitude,
            driverDestinationLongitude,
            routePolyline,
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude,
            radiusKm);
    }
}
