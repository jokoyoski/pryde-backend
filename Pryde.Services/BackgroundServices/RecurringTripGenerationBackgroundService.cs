using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.BackgroundServices;

public sealed class RecurringTripGenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RecurringTripSettings> settings,
    ILogger<RecurringTripGenerationBackgroundService> logger)
    : BackgroundService
{
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(
        settings.Value.GenerationIntervalMinutes);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RunCycleSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(_processingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunCycleSafelyAsync(stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public async Task<int> ProcessAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IRecurringTripService>();
        return await service.GenerateOccurrencesAsync(
            utcNow, cancellationToken);
    }

    private async Task RunCycleSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Recurring trip generation cycle failed.");
        }
    }
}
