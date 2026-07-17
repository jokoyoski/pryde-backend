using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.DependencyInjection;

public static class DojahServiceCollectionExtension
{
    public static IServiceCollection AddDojahIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<DojahSettings>, DojahSettingsValidator>();
        services.AddOptions<DojahSettings>()
            .Bind(configuration.GetSection(DojahSettings.SectionName))
            .ValidateOnStart();
        services.AddScoped<IDojahKycService, DojahKycService>();
        return services;
    }
}
