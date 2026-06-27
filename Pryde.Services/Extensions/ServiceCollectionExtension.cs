using Microsoft.Extensions.DependencyInjection;
using Pryde.Services.Service.Interface;
using Pryde.Services.Security.Interface;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Service.Implementation;
namespace Pryde.Services.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddServices(
        this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IRoleService, RoleService>();

        return services;
    }
}
