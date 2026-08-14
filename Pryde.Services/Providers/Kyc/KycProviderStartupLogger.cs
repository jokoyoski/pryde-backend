using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pryde.Services.Providers.Kyc;

public sealed class KycProviderStartupLogger(
    IServiceScopeFactory scopeFactory,
    ILogger<KycProviderStartupLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider
            .GetRequiredService<IKycProviderResolver>()
            .ResolveActive();
        logger.LogInformation(
            "KYC active provider resolved to {ProviderName}.",
            provider.Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
