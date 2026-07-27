using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class DriverDashboardService(
    IProfileService profileService,
    IWalletService walletService,
    ITripService tripService,
    IUnitOfWork unitOfWork) : IDriverDashboardService
{
    private const int RecentTripCount = 5;

    public async Task<DriverDashboardResponseDto> GetAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var driverProfile = await profileService.GetMineAsync(
            driverId,
            cancellationToken);

        var driverTrips = await tripService.GetMineAsync(
            driverId,
            cancellationToken);

        var walletBalance = 0m;
        var todayEarnings = 0m;
        var totalEarnings = 0m;

        try
        {
            var wallet = await walletService.GetMineAsync(
                driverId,
                cancellationToken);

            walletBalance = wallet.Balance;

            var walletTransactions = await walletService.GetTransactionsAsync(
                driverId,
                cancellationToken);

            var earningsTransactions = walletTransactions
                .Where(transaction =>
                    transaction.Type == WalletTransactionType.EscrowRelease)
                .ToList();

            totalEarnings = earningsTransactions.Sum(transaction =>
                transaction.Amount);

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            todayEarnings = earningsTransactions
                .Where(transaction =>
                    transaction.CreatedAt >= today &&
                    transaction.CreatedAt < tomorrow)
                .Sum(transaction => transaction.Amount);
        }
        catch (NotFoundException)
        {
            walletBalance = 0m;
            todayEarnings = 0m;
            totalEarnings = 0m;
        }

        var now = DateTime.UtcNow;

        var upcomingTrip = driverTrips
            .Where(trip =>
                trip.DepartureTime > now &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled)
            .OrderBy(trip => trip.DepartureTime)
            .FirstOrDefault();

        var recentTrips = driverTrips
            .Where(trip => trip.Status == TripStatus.Completed)
            .OrderByDescending(trip => trip.DepartureTime)
            .Take(RecentTripCount)
            .ToList();

        var pendingBookingRequestCount = await unitOfWork.TripBookings
            .CountPendingByDriverIdAsync(driverId, cancellationToken);

        return new DriverDashboardResponseDto
        {
            DriverProfile = driverProfile,
            WalletBalance = walletBalance,
            TodayEarnings = todayEarnings,
            TotalEarnings = totalEarnings,
            UpcomingTrip = upcomingTrip,
            RecentTrips = recentTrips,
            PendingBookingRequestCount = pendingBookingRequestCount
        };
    }
}
