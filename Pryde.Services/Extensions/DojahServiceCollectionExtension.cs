using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pryde.Services.Providers.Dojah;
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
        services.AddHttpClient<IDojahApiClient, DojahApiClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<DojahSettings>>().Value;
            if (Uri.TryCreate($"{settings.BaseUrl.TrimEnd('/')}/", UriKind.Absolute, out var baseAddress))
            {
                client.BaseAddress = baseAddress;
            }
        });
        services.AddScoped<IDojahKycService, DojahKycService>();
        return services;
    }
}
