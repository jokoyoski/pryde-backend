using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.BackgroundServices;

public sealed class TripAutoCompletionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TripAutoCompletionBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan ProcessingInterval =
        TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RunCycleSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(ProcessingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public async Task ProcessEligibleTripsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> tripIds;

        using (var scope = scopeFactory.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            tripIds = await unitOfWork.Trips
                .GetExpiredConfirmationTripIdsAsync(
                    DateTime.UtcNow,
                    cancellationToken);
        }

        foreach (var tripId in tripIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = scopeFactory.CreateScope();
                var financialService = scope.ServiceProvider
                    .GetRequiredService<IFinancialService>();
                await financialService.AutoCompleteTripAsync(
                    tripId,
                    cancellationToken);
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
                    "Automatic trip completion failed for trip {TripId}.",
                    tripId);
            }
        }
    }

    private async Task RunCycleSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessEligibleTripsAsync(cancellationToken);
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
                "Automatic trip completion cycle failed.");
        }
    }
}
