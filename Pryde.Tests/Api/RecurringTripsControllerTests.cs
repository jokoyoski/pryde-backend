using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Pryde.Api.Controllers.V1;
using Pryde.Domain.Constants;

namespace Pryde.Tests.Api;

public class RecurringTripsControllerTests
{
    [Fact]
    public void DriverControllerRequiresVerifiedDriver()
    {
        var authorize = typeof(DriverRecurringTripsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Driver", authorize.Roles);
        Assert.Equal("EmailVerified", authorize.Policy);
    }

    [Fact]
    public void PassengerControllerRequiresVerifiedPassenger()
    {
        var authorize = typeof(PassengerRecurringTripsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Passenger", authorize.Roles);
        Assert.Equal("EmailVerified", authorize.Policy);
    }

    [Fact]
    public void AdminControllerRequiresAdminOrSuperAdmin()
    {
        var authorize = typeof(AdminRecurringTripsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(RoleNames.AdminOrSuperAdmin, authorize.Roles);
    }

    [Fact]
    public void RecurringRoutesAreAdditiveAndUseExpectedTemplates()
    {
        var driverRoute = typeof(DriverRecurringTripsController)
            .GetCustomAttribute<RouteAttribute>();
        var adminRoute = typeof(AdminRecurringTripsController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal(
            "api/v{version:apiVersion}/recurring-trips",
            driverRoute?.Template);
        Assert.Equal(
            "api/v{version:apiVersion}/admin/recurring-trips",
            adminRoute?.Template);
    }

    [Theory]
    [InlineData(nameof(PassengerRecurringTripsController.Save),
        "{recurringTripId:guid}/save", typeof(HttpPostAttribute))]
    [InlineData(nameof(PassengerRecurringTripsController.GetSaved),
        "saved", typeof(HttpGetAttribute))]
    [InlineData(nameof(PassengerRecurringTripsController.RemoveSaved),
        "{recurringTripId:guid}/save", typeof(HttpDeleteAttribute))]
    public void SavedRecurringTripEndpointsUseRequiredTemplates(
        string methodName,
        string template,
        Type attributeType)
    {
        var method = typeof(PassengerRecurringTripsController)
            .GetMethod(methodName);
        var route = method?.GetCustomAttributes(attributeType, false)
            .Cast<HttpMethodAttribute>()
            .Single();

        Assert.NotNull(route);
        Assert.Equal(template, route.Template);
    }
}
