using Microsoft.Extensions.DependencyInjection;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Security.Interface;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Storage.Implementation;
using Pryde.Services.Storage.Interface;
namespace Pryde.Services.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddServices(
        this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOnboardingStatusService, OnboardingStatusService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IProfileService, ProfileService>();

        services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
        services.AddScoped<IVehicleDocumentService, VehicleDocumentService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<ITripBookingService, TripBookingService>();
        services.AddScoped<IDriverDashboardService, DriverDashboardService>();
        services.AddScoped<IAdminListingService, AdminListingService>();
        services.AddScoped<IFinancialService, FinancialService>();
        services.AddScoped<IDriverWithdrawalService, DriverWithdrawalService>();
        services.AddScoped<IAdminWalletService, AdminWalletService>();
        services.AddScoped<IAdminPortalService, AdminPortalService>();

        services.AddScoped<IFareCalculator, FareCalculator>();
        services.AddScoped<IRouteMatchingService, RouteMatchingService>();
        return services;
    }
}
