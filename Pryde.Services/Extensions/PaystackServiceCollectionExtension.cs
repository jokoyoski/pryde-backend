using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.DependencyInjection;

public static class PaystackServiceCollectionExtension
{
    public static IServiceCollection AddPaystackIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<
            IValidateOptions<PaystackSettings>,
            PaystackSettingsValidator>();

        services.AddOptions<PaystackSettings>()
            .Bind(configuration.GetSection(
                PaystackSettings.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IPaystackClient, PaystackClient>(
    (serviceProvider, httpClient) =>
    {
        var settings = serviceProvider
            .GetRequiredService<IOptions<PaystackSettings>>()
            .Value;

        httpClient.BaseAddress = new Uri(
            settings.BaseUrl.TrimEnd('/') + "/");
    });

        services.AddScoped<
            IDriverBankAccountService,
            DriverBankAccountService>();

        return services;
    }
}