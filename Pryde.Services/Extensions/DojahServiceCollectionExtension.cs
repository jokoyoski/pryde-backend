using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pryde.Services.Providers.Dojah;
using Pryde.Services.Providers.Kyc;
using Pryde.Services.Providers.SmileId;
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
        services.AddSingleton<IValidateOptions<SmileIdSettings>, SmileIdSettingsValidator>();
        services.AddSingleton<IValidateOptions<KycSettings>, KycSettingsValidator>();
        services.AddOptions<KycSettings>()
            .Bind(configuration.GetSection(KycSettings.SectionName))
            .ValidateOnStart();
        services.AddOptions<DojahSettings>()
            .Bind(configuration.GetSection(DojahSettings.SectionName))
            .ValidateOnStart();
        services.AddOptions<SmileIdSettings>()
            .Bind(configuration.GetSection(SmileIdSettings.SectionName))
            .ValidateOnStart();
        services.AddHttpClient<IDojahApiClient, DojahApiClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<DojahSettings>>().Value;
            if (Uri.TryCreate($"{settings.BaseUrl.TrimEnd('/')}/", UriKind.Absolute, out var baseAddress))
            {
                client.BaseAddress = baseAddress;
            }
        });
        services.AddScoped<DojahKycProvider>();
        services.AddScoped<IKycProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DojahKycProvider>());
        services.AddScoped<IDojahKycService>(serviceProvider =>
            new DojahKycService(
                serviceProvider.GetRequiredService<DojahKycProvider>()));
        services.AddHttpClient<ISmileIdApiClient, SmileIdApiClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<SmileIdSettings>>().Value;
            var baseUrl = settings.Environment == SmileIdSettings.Production
                ? settings.ProductionBaseUrl
                : settings.SandboxBaseUrl;
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress))
            {
                client.BaseAddress = baseAddress;
            }
        });
        services.AddScoped<SmileIdKycProvider>();
        services.AddScoped<IKycProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<SmileIdKycProvider>());
        services.AddScoped<ISmileIdKycService>(serviceProvider =>
            serviceProvider.GetRequiredService<SmileIdKycProvider>());
        services.AddScoped<IKycProviderResolver, KycProviderResolver>();
        services.AddScoped<IKycProviderService, KycProviderService>();
        services.AddHostedService<KycProviderStartupLogger>();
        return services;
    }
}
