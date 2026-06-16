using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pryde.Persistence.Context;
using Pryde.Persistence.Extension;

namespace Pryde.Persistence.DependencyInjection;

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
                action =>
                    action.MigrationsAssembly(typeof(PrydeDbContext).Assembly.FullName)
                          .EnableRetryOnFailure(
                              maxRetryCount: 5,
                              maxRetryDelay: TimeSpan.FromSeconds(30),
                              errorCodesToAdd: null)));

        return services;
    }
}