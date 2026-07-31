using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Pryde.Api.Authorization;
using Pryde.Api.Controllers.V1;
using Pryde.Api.Extension;
using Pryde.Api.Extensions;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;
using Swashbuckle.AspNetCore.Swagger;

namespace Pryde.Tests.Api;

public class ApiConfigurationTests
{
    [Fact]
    public void RoleSelectionRequiresAuthentication()
    {
        var action = typeof(AuthController).GetMethod(nameof(AuthController.SelectRoles));

        Assert.NotNull(action);
        Assert.Contains(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => string.IsNullOrEmpty(attribute.Roles));
    }

    [Theory]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.GetAll))]
    [InlineData(typeof(AdminDriversController), nameof(AdminDriversController.GetAll))]
    [InlineData(typeof(AdminTripsController), nameof(AdminTripsController.GetAll))]
    [InlineData(typeof(AdminBookingsController), nameof(AdminBookingsController.GetAll))]
    [InlineData(typeof(AdminFinanceController), nameof(AdminFinanceController.GetWalletTransactions))]
    [InlineData(typeof(AdminWalletsController), nameof(AdminWalletsController.Fund))]
    [InlineData(typeof(AdminLedgerController), nameof(AdminLedgerController.GetAll))]
    [InlineData(typeof(AdminEscrowsController), nameof(AdminEscrowsController.GetAll))]
    [InlineData(typeof(AdminNotificationsController), nameof(AdminNotificationsController.GetAll))]
    [InlineData(typeof(AdminNotificationsController), nameof(AdminNotificationsController.Get))]
    [InlineData(typeof(KycController), nameof(KycController.GetAdminKyc))]
    [InlineData(typeof(KycController), nameof(KycController.ApproveKyc))]
    [InlineData(typeof(KycController), nameof(KycController.RejectKyc))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.GetAdminVehicles))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.ActivateVehicle))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.DeactivateVehicle))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.RejectVehicle))]
    [InlineData(typeof(VehicleDocumentController), nameof(VehicleDocumentController.GetAdminVehicleDocuments))]
    public void AdminResourceActionsRequireAdminOrSuperAdmin(Type controllerType, string actionName)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);

        var authorize = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Contains(authorize,
            attribute => attribute.Roles == RoleNames.AdminOrSuperAdmin);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Driver", false)]
    [InlineData("Passenger", false)]
    public async Task AdminResourcePolicyAllowsOnlyAdminRoles(string role, bool expected)
    {
        var authorizeData = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>();

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
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Driver", false)]
    [InlineData("Passenger", false)]
    public async Task AdminWalletFundingAllowsOnlyAdminRoles(
        string role,
        bool expected)
    {
        var authorizeData = typeof(AdminWalletsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();

        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            authorizeData);
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, role)
                ],
                "Test"));
        var result = await provider
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(
                principal,
                null,
                policy!);

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
        var controllerAuthorize = typeof(KycController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Null(controllerAuthorize.Policy);

        var config = typeof(KycController).GetMethod(nameof(KycController.GetDojahConfig));
        Assert.NotNull(config);
        Assert.Contains(
            config.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.EmailVerified);

        var webhook = typeof(KycController).GetMethod(nameof(KycController.ProcessDojahWebhook));
        Assert.NotNull(webhook);
        Assert.NotEmpty(webhook.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Theory]
    [InlineData(nameof(KycController.UploadDocuments))]
    [InlineData(nameof(KycController.Submit))]
    [InlineData(nameof(KycController.GetMine))]
    [InlineData(nameof(KycController.GetDojahConfig))]
    public void CustomerKycActionsRequireEmailVerification(string actionName)
    {
        var action = typeof(KycController).GetMethod(actionName);
        Assert.NotNull(action);
        Assert.Contains(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.EmailVerified);
    }

    [Theory]
    [InlineData(typeof(VehicleController), nameof(VehicleController.Create))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.Update))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.AddImages))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.DeleteImage))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.Delete))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.UpdateDetails))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.UpdateMedia))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.UpdateCapacityExtras))]
    [InlineData(typeof(VehicleController), nameof(VehicleController.Submit))]
    [InlineData(typeof(VehicleDocumentController), nameof(VehicleDocumentController.Upload))]
    [InlineData(typeof(VehicleDocumentController), nameof(VehicleDocumentController.Delete))]
    public void VehicleCustomerMutationsRequireEmailVerification(
        Type controllerType,
        string actionName)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);
        Assert.Contains(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.EmailVerified);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DojahConfigRequiresVerifiedEmail(
        bool isEmailVerified,
        bool expected)
    {
        var result = await AuthorizeKycActionAsync(
            nameof(KycController.GetDojahConfig),
            isEmailVerified,
            "Passenger");

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Passenger", false)]
    public async Task AdminKycEndpointUsesRolesWithoutCustomerEmailVerification(
        string role,
        bool expected)
    {
        var result = await AuthorizeKycActionAsync(
            nameof(KycController.GetAdminKyc),
            isEmailVerified: false,
            role);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Passenger", false)]
    public async Task AdminKycDetailEndpointUsesRolesWithoutCustomerEmailVerification(
        string role,
        bool expected)
    {
        var result = await AuthorizeKycActionAsync(
            nameof(KycController.GetAdminKycById),
            isEmailVerified: false,
            role);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task UnauthenticatedRequestReturnsUnauthorizedApiError()
    {
        using var provider = CreateAuthenticationServices();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext,
            scheme,
            options,
            new AuthenticationProperties());

        await options.Events.OnChallenge(challengeContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        var response = await ReadErrorResponseAsync(httpContext.Response);
        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized.", response.Message);
    }

    [Fact]
    public async Task ForbiddenResponseUsesGenericApiError()
    {
        using var provider = CreateAuthenticationServices();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var forbiddenContext = new ForbiddenContext(
            httpContext,
            scheme,
            options);

        await options.Events.OnForbidden(forbiddenContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var response = await ReadErrorResponseAsync(httpContext.Response);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal("Forbidden.", response.Message);
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
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdminWalletService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationService>());
    }

    [Fact]
    public void SwaggerContainsExistingResourceAndAdminEndpointsWithoutDuplicates()
    {
        var builder = WebApplication.CreateBuilder();
        var services = builder.Services;
        services.AddLogging();
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()))
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
        Assert.DoesNotContain("/api/v1/admin/users/paged", paths);
        Assert.Contains("/api/v1/admin/kyc", paths);
        Assert.Contains("/api/v1/admin/vehicles", paths);
        Assert.Contains("/api/v1/vehicles/{vehicleId}/details", paths);
        Assert.Contains("/api/v1/vehicles/{vehicleId}/media", paths);
        Assert.Contains("/api/v1/vehicles/{vehicleId}/capacity-extras", paths);
        Assert.Contains("/api/v1/vehicles/{vehicleId}/submit", paths);
        Assert.Contains("/api/v1/admin/vehicle-documents", paths);
        Assert.Contains("/api/v1/admin/staff", paths);
        Assert.Contains("/api/v1/admin/staff/invite", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}/activate", paths);
        Assert.Contains("/api/v1/admin/staff/{staffId}/deactivate", paths);
        Assert.Contains("/api/v1/admin/dashboard", paths);
        Assert.Contains("/api/v1/admin/drivers", paths);
        Assert.Contains("/api/v1/admin/drivers/{driverId}", paths);
        Assert.Contains("/api/v1/admin/trips", paths);
        Assert.Contains("/api/v1/admin/trips/{tripId}", paths);
        Assert.Contains("/api/v1/admin/bookings", paths);
        Assert.Contains("/api/v1/admin/bookings/{bookingId}", paths);
        Assert.Contains("/api/v1/admin/finance/summary", paths);
        Assert.Contains("/api/v1/admin/wallet-transactions", paths);
        Assert.Contains("/api/v1/admin/wallets/fund", paths);
        Assert.Contains("/api/v1/admin/escrows", paths);
        Assert.Contains("/api/v1/admin/escrows/{escrowId}", paths);
        Assert.Contains("/api/v1/admin/ledger/transactions", paths);
        Assert.Contains("/api/v1/admin/ledger/transactions/{transactionId}", paths);
        Assert.Contains("/api/v1/notifications", paths);
        Assert.Contains("/api/v1/notifications/unread-count", paths);
        Assert.Contains("/api/v1/notifications/{notificationId}/read", paths);
        Assert.Contains("/api/v1/notifications/read-all", paths);
        Assert.Contains("/api/v1/admin/notifications", paths);
        Assert.Contains("/api/v1/admin/notifications/{notificationId}", paths);
        Assert.Contains("/api/v1/kyc/documents", paths);
        Assert.Contains("/api/v1/kyc/mine", paths);
        Assert.Contains("/api/v1/kyc/dojah/config", paths);
        Assert.Contains("/api/v1/kyc/dojah/webhook", paths);
        Assert.Contains("/api/v1/auth/email-verification/resend", paths);
        Assert.Contains("/api/v1/auth/email-verification/verify", paths);
        Assert.Contains("/api/v1/auth/verification-status", paths);
        Assert.Contains("/api/v1/auth/onboarding-status", paths);
        Assert.Contains("/api/v1/auth/roles/select", paths);
        Assert.Contains("/api/v1/kyc/submit", paths);
        Assert.Contains("/api/v1/admin/kyc/{userId}/approve", paths);
        Assert.Contains("/api/v1/admin/kyc/{userId}/reject", paths);
        Assert.Contains("/api/v1/admin/vehicles/{id}/activate", paths);
        Assert.Contains("/api/v1/admin/vehicles/{id}/deactivate", paths);
        Assert.Contains("/api/v1/admin/vehicles/{vehicleId}/reject", paths);

        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/users"].Operations.Single().Key);
        Assert.Single(document.Paths["/api/v1/admin/users"].Operations);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/kyc"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/kyc/{userId}/approve"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/kyc/{userId}/reject"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/vehicles"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/vehicles/{id}/activate"].Operations.Single().Key);
        Assert.Equal(OperationType.Post, document.Paths["/api/v1/admin/vehicles/{id}/deactivate"].Operations.Single().Key);
        Assert.Equal(
            OperationType.Post,
            document.Paths["/api/v1/admin/vehicles/{vehicleId}/reject"].Operations.Single().Key);
        Assert.Equal(OperationType.Get, document.Paths["/api/v1/admin/vehicle-documents"].Operations.Single().Key);
        Assert.Equal(
            OperationType.Post,
            document.Paths["/api/v1/auth/roles/select"].Operations.Single().Key);
        Assert.Equal(
            OperationType.Post,
            document.Paths["/api/v1/kyc/submit"].Operations.Single().Key);
        Assert.DoesNotContain(
            "roles",
            document.Components.Schemas[nameof(RegisterRequestDto)].Properties.Keys);
        Assert.Equal(
            "string",
            document.Components.Schemas["OnboardingStage"].Type);
        Assert.Equal(
            [
                "RoleSelection",
                "IdentityVerification",
                "DriverDocuments",
                "VehicleInformation",
                "SubmittedForReview",
                "Completed"
            ],
            document.Components.Schemas["OnboardingStage"].Enum
                .Cast<OpenApiString>()
                .Select(value => value.Value)
                .ToList());
        Assert.Equal(
            "string",
            document.Components.Schemas["DriverVerificationStatus"].Type);
        Assert.Contains(
            document.Components.Schemas["DriverVerificationStatus"].Enum,
            value => value is OpenApiString openApiString &&
                     openApiString.Value == "ResubmissionRequired");
    }

    [Fact]
    public async Task AdminWalletFundingIsHiddenInProduction()
    {
        var service = new TestAdminWalletService();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = "Production"
        };
        var controller = new AdminWalletsController(
            service,
            environment);

        var result = await controller.Fund(
            new AdminFundWalletRequest
            {
                UserId = Guid.NewGuid(),
                Amount = 100m
            },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, service.CallCount);
    }

    private static async Task<AuthorizationResult> AuthorizeKycActionAsync(
        string actionName,
        bool isEmailVerified,
        string role)
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User
        {
            Email = "kyc-policy@test.local",
            PhoneNumber = "08000000000",
            IsEmailVerified = isEmailVerified
        };
        ((TestUserRepository)unitOfWork.Users).Items.Add(user);

        using var provider = CreateAuthenticationServices(unitOfWork);
        using var scope = provider.CreateScope();
        var authorizeData = typeof(KycController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>()
            .Concat(typeof(KycController)
                .GetMethod(actionName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<IAuthorizeData>());
        var policy = await AuthorizationPolicy.CombineAsync(
            scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>(),
            authorizeData);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            "Test"));

        return await scope.ServiceProvider
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, policy!);
    }

    private static ServiceProvider CreateAuthenticationServices(
        IUnitOfWork? unitOfWork = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = "test-signing-key-that-is-long-enough-for-hmac",
                ["JwtSettings:Issuer"] = "test-issuer",
                ["JwtSettings:Audience"] = "test-audience"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        if (unitOfWork is not null)
        {
            services.AddSingleton(unitOfWork);
        }
        services.AddAuthenticationConfiguration(configuration);

        return services.BuildServiceProvider();
    }

    private static async Task<ErrorResponseDto> ReadErrorResponseAsync(
        HttpResponse response)
    {
        response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<ErrorResponseDto>(
            response.Body))!;
    }

    private sealed class TestAdminWalletService : IAdminWalletService
    {
        public int CallCount { get; private set; }

        public Task<AdminFundWalletResponseDto> FundWalletAsync(
            AdminFundWalletRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(
                new AdminFundWalletResponseDto());
        }
    }

}
