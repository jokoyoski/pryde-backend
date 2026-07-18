using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pryde.Persistence.Context;
using Pryde.Persistence.Extension;
using Pryde.Persistence.Repository.Implementations;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Persistence.Settings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration
            .GetDbConnectionStringBuilder()
            .ConnectionString;

        services.AddDbContext<PrydeDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsAssembly(
                            typeof(PrydeDbContext).Assembly.FullName)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null)));

        services.AddRepositories();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IKycVerificationRepository, KycVerificationRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleDocumentRepository, VehicleDocumentRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetCodeRepository, PasswordResetCodeRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<ITripBookingRepository, TripBookingRepository>();
        services.AddScoped<IRecurringTripRepository, RecurringTripRepository>();
        services.AddScoped<ITripSubscriptionRepository, TripSubscriptionRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<IVirtualAccountRepository, VirtualAccountRepository>();
        services.AddScoped<IVehicleImageRepository, VehicleImageRepository>();
        services.AddScoped<IAdminListingRepository, AdminListingRepository>();
        services.AddScoped<IEscrowRepository, EscrowRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
