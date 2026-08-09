using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Api.Controllers.V1;

namespace Pryde.Tests.Api;

public class PaystackWalletFundingEndpointTests
{
    [Fact]
    public void VerifyEndpointUsesWalletRouteAndAuthenticatedPolicy()
    {
        var controllerType = typeof(WalletController);
        var action = controllerType.GetMethod(
            nameof(WalletController.VerifyPaystackFunding))!;
        var route = Assert.Single(
            controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>());
        var post = Assert.Single(
            action.GetCustomAttributes(typeof(HttpPostAttribute), true)
                .Cast<HttpPostAttribute>());
        var authorize = Assert.Single(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("api/v{version:apiVersion}/wallet", route.Template);
        Assert.Equal("paystack/verify", post.Template);
        Assert.Equal(
            AuthorizationPolicies.EmailVerified,
            authorize.Policy);
    }

    [Fact]
    public void FundingRequestEndpointUsesRequiredAuthenticatedWalletRoute()
    {
        var controllerType = typeof(WalletController);
        var action = controllerType.GetMethod(
            nameof(WalletController.CreateFundingRequest))!;
        var post = Assert.Single(
            action.GetCustomAttributes(typeof(HttpPostAttribute), true)
                .Cast<HttpPostAttribute>());

        Assert.Equal("paystack/funding-requests", post.Template);
        Assert.NotNull(controllerType.GetCustomAttributes(
                typeof(AuthorizeAttribute),
                true)
            .Single());
    }

    [Fact]
    public void WebhookEndpointIsAnonymousAndUsesSinglePaystackRoute()
    {
        var controllerType = typeof(PaystackWebhooksController);
        var route = Assert.Single(
            controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>());
        var allowAnonymous = Assert.Single(
            controllerType.GetCustomAttributes(
                typeof(AllowAnonymousAttribute),
                true));

        Assert.Equal(
            "api/v{version:apiVersion}/webhooks/paystack",
            route.Template);
        Assert.NotNull(allowAnonymous);
    }
}
