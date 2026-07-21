using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Pryde.Api.Controllers.V1;
using Pryde.Api.Extension;
using Pryde.Api.Extensions;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;
using Swashbuckle.AspNetCore.Swagger;

namespace Pryde.Tests.Api;

public class ApiConfigurationTests
{
    [Theory]
    [InlineData(typeof(ProfileController), nameof(ProfileController.GetAllUsers))]
    [InlineData(typeof(ProfileController), nameof(ProfileController.GetPagedUsers))]
    [InlineData(typeof(KycController), nameof(KycController.GetAdminKyc))]
    [InlineData(typeof(KycController), nameof(KycController.ApproveKyc))]
    [InlineData(typeof(KycController), nameof(KycController.RejectKyc))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.GetAdminVehicles))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.ActivateVehicle))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.DeactivateVehicle))]
    [InlineData(typeof(VehicleDocumentController), nameof(VehicleDocumentController.GetAdminVehicleDocuments))]
    public void AdminResourceActionsRequireAdminOrSuperAdmin(Type controllerType, string actionName)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);

        var authorize = action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin,SuperAdmin", authorize.Roles);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Driver", false)]
    public async Task AdminResourcePolicyAllowsOnlyAdminRoles(string role, bool expected)
    {
        var authorizeData = typeof(ProfileController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>()
            .Concat(typeof(ProfileController)
                .GetMethod(nameof(ProfileController.GetAllUsers))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<IAuthorizeData>());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            "Test"));

        var result = await authorizationService.AuthorizeAsync(principal, null, policy!);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", false)]
    [InlineData("Passenger", false)]
    public async Task StaffManagementAllowsOnlySuperAdmin(string role, bool expected)
    {
        var authorizeData = typeof(AdminStaffController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            "Test"));

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, policy!);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void FinancialReportingControllersExposeOnlyGetActions()
    {
        var controllerTypes = new[]
        {
            typeof(AdminFinanceController), typeof(AdminEscrowsController), typeof(AdminLedgerController)
        };

        foreach (var method in controllerTypes.SelectMany(type => type.GetMethods()
                     .Where(method => method.DeclaringType == type)))
        {
            var httpAttributes = method.GetCustomAttributes(true)
                .Where(attribute => attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal))
                .ToList();
            Assert.All(httpAttributes, attribute => Assert.Equal("HttpGetAttribute", attribute.GetType().Name));
        }
    }

    [Fact]
    public void DojahConfigRequiresAuthenticationAndWebhookAllowsProviderCalls()
    {
        Assert.NotEmpty(typeof(KycController).GetCustomAttributes(typeof(AuthorizeAttribute), true));

        var webhook = typeof(KycController).GetMethod(nameof(KycController.ProcessDojahWebhook));
        Assert.NotNull(webhook);
        Assert.NotEmpty(webhook.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public void TripServicesResolveFromDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IUnitOfWork>(new TestUnitOfWork());
        services.AddSingleton<IOptions<PricingSettings>>(Options.Create(TestData.Pricing));
        services.AddServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITripService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITripBookingService>());
    }

    [Fact]
    public void SwaggerContainsExistingResourceAndAdminEndpointsWithoutDuplicates()
    {
        var builder = WebApplication.CreateBuilder();
        var services = builder.Services;
        services.AddLogging();
        services.AddControllers()
            .AddApplicationPart(typeof(TripsController).Assembly);
        services.AddEndpointsApiExplorer();
        services.AddApiVersioningConfiguration();
        services.AddSwaggerConfiguration();

        using var app = builder.Build();
        app.MapControllers();
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var paths = document.Paths.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/api/v1/trips", paths);
        Assert.Contains("/api/v1/trips/{tripId}", paths);
        Assert.Contains("/api/v1/trips/mine", paths);
        Assert.Contains("/api/v1/trips/{tripId}/cancel", paths);
        Assert.Contains("/api/v1/trip-bookings", paths);
        Assert.Contains("/api/v1/trip-bookings/mine", paths);
        Assert.Contains("/api/v1/trips/{tripId}/booking-requests", paths);
        Assert.Contains("/api/v1/trips/{tripId}/passengers", paths);
        Assert.Contains("/api/v1/trip-bookings/{bookingId}/approve", paths);
        Assert.Contains("/api/v1/trip-bookings/{bookingId}/decline", paths);
        Assert.Contains("/api/v1/trip-bookings/{bookingId}/cancel", paths);
        Assert.Contains("/api/v1/trip-bookings/{bookingId}/pay", paths);
        Assert.Contains("/api/v1/trips/{tripId}/complete", paths);
        Assert.Contains("/api/v1/wallet/mine", paths);
        Assert.Contains("/api/v1/wallet/mine/transactions", paths);
        Assert.Contains("/api/v1/virtual-accounts/mine", paths);
        Assert.Contains("/api/v1/admin/users", paths);
        Assert.Contains("/api/v1/admin/users/paged", paths);
        Assert.Contains("/api/v1/admin/kyc", paths);
        Assert.Contains("/api/v1/admin/vehicles", paths);
        Assert.Contains("/api/v1/admin/vehicle-documents", paths);
        Assert.Contains("/api/v1/admin/staff", paths);
        Assert.Contains("/api/v1/admin/staff/invite", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}/activate", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}/deactivate", paths);
        Assert.Contains("/api/v1/admin/dashboard", paths);
        Assert.Contains("/api/v1/admin/drivers", paths);
        Assert.Contains("/api/v1/admin/drivers/{driverId}", paths);
        Assert.Contains("/api/v1/admin/finance/summary", paths);
        Assert.Contains("/api/v1/admin/wallet-transactions", paths);
        Assert.Contains("/api/v1/admin/escrows", paths);
        Assert.Contains("/api/v1/admin/escrows/{escrowId}", paths);
        Assert.Contains("/api/v1/admin/ledger/transactions", paths);
        Assert.Contains("/api/v1/admin/ledger/transactions/{transactionId}", paths);
        Assert.Contains("/api/v1/kyc/documents", paths);
        Assert.Contains("/api/v1/kyc/mine", paths);
        Assert.Contains("/api/v1/kyc/dojah/config", paths);
        Assert.Contains("/api/v1/kyc/dojah/webhook", paths);
        Assert.Contains("/api/v1/auth/email-verification/resend", paths);
        Assert.Contains("/api/v1/auth/email-verification/verify", paths);
        Assert.Contains("/api/v1/auth/verification-status", paths);
        Assert.Contains("/api/v1/admin/kyc/{userId}/approve", paths);
        Assert.Contains("/api/v1/admin/kyc/{userId}/reject", paths);
        Assert.Contains("/api/v1/admin/vehicles/{id}/activate", paths);
        Assert.Contains("/api/v1/admin/vehicles/{id}/deactivate", paths);

        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/users"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/users/paged"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/kyc"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/kyc/{userId}/approve"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/kyc/{userId}/reject"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/vehicles"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/vehicles/{id}/activate"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/vehicles/{id}/deactivate"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/vehicle-documents"].Operations.Single().Key);
    }
}
