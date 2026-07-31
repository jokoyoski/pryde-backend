using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.BackgroundServices;

public sealed class BookingPaymentExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BookingPaymentSettings> options,
    ILogger<BookingPaymentExpiryBackgroundService> logger)
    : BackgroundService
{
    private readonly TimeSpan _processingInterval =
        TimeSpan.FromMinutes(
            options.Value.ExpiryCheckIntervalMinutes);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RunCycleSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            _processingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await RunCycleSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public async Task ProcessExpiredBookingsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> bookingIds;
        var utcNow = DateTime.UtcNow;

        using (var discoveryScope =
               scopeFactory.CreateScope())
        {
            var unitOfWork = discoveryScope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            bookingIds = await unitOfWork.TripBookings
                .GetExpiredUnpaidApprovedBookingIdsAsync(
                    utcNow,
                    cancellationToken);
        }

        foreach (var bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var bookingScope =
                    scopeFactory.CreateScope();
                var financialService =
                    bookingScope.ServiceProvider
                        .GetRequiredService<IFinancialService>();
                await financialService
                    .ExpireUnpaidApprovedBookingAsync(
                        bookingId,
                        utcNow,
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
                    "Booking payment expiry failed for booking {BookingId}.",
                    bookingId);
            }
        }
    }

    private async Task RunCycleSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessExpiredBookingsAsync(
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
                "Booking payment expiry cycle failed.");
        }
    }
}
