using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.V1;
using Pryde.Domain.Constants;

namespace Pryde.Tests.Api;

public class AdminUserDeletionEndpointTests
{
    [Fact]
    public void DeleteEndpointUsesExpectedRouteAndSuperAdminAuthorization()
    {
        var controllerType = typeof(AdminUsersController);
        var action = controllerType.GetMethod(nameof(AdminUsersController.Delete))!;
        var route = Assert.Single(
            controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>());
        var delete = Assert.Single(
            action.GetCustomAttributes(typeof(HttpDeleteAttribute), true)
                .Cast<HttpDeleteAttribute>());
        var authorize = Assert.Single(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("api/v{version:apiVersion}/admin/users", route.Template);
        Assert.Null(delete.Template);
        Assert.Equal(RoleNames.SuperAdmin, authorize.Roles);
    }
}
