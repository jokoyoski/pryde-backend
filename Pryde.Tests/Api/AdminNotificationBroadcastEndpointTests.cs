using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.V1;
using Pryde.Domain.Constants;

namespace Pryde.Tests.Api;

public class AdminNotificationBroadcastEndpointTests
{
    [Fact]
    public void BroadcastEndpointUsesExpectedRouteAndAdminAuthorization()
    {
        var controllerType = typeof(AdminNotificationsController);
        var action = controllerType.GetMethod(nameof(AdminNotificationsController.Broadcast))!;
        var route = Assert.Single(
            controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>());
        var post = Assert.Single(
            action.GetCustomAttributes(typeof(HttpPostAttribute), true)
                .Cast<HttpPostAttribute>());
        var authorize = Assert.Single(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("api/v{version:apiVersion}/admin/notifications", route.Template);
        Assert.Equal("broadcast", post.Template);
        Assert.Equal(RoleNames.AdminOrSuperAdmin, authorize.Roles);
    }
}
