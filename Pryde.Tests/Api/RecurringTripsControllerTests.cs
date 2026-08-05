using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
