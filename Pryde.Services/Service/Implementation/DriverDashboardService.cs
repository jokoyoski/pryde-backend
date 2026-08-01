using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class DriverDashboardService(
    IProfileService profileService,
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

        var walletBalanceResult = await unitOfWork.Wallets
            .GetBalanceByUserIdAsync(driverId, cancellationToken);
        var walletBalance = walletBalanceResult ?? 0m;
        var todayEarnings = 0m;
        var thisWeekEarnings = 0m;
        var totalEarnings = 0m;
        var utcNow = DateTime.UtcNow;

        if (walletBalanceResult.HasValue)
        {
            var todayStart = utcNow.Date;
            var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
            var weekStart = todayStart.AddDays(-daysSinceMonday);

            todayEarnings = await unitOfWork.WalletTransactions
                .SumByUserIdAndTypeAsync(
                    driverId,
                    WalletTransactionType.EscrowRelease,
                    todayStart,
                    utcNow,
                    cancellationToken);
            thisWeekEarnings = await unitOfWork.WalletTransactions
                .SumByUserIdAndTypeAsync(
                    driverId,
                    WalletTransactionType.EscrowRelease,
                    weekStart,
                    utcNow,
                    cancellationToken);
            totalEarnings = await unitOfWork.WalletTransactions
                .SumByUserIdAndTypeAsync(
                    driverId,
                    WalletTransactionType.EscrowRelease,
                    null,
                    utcNow,
                    cancellationToken);
        }

        var completedTripCount = await unitOfWork.Trips
            .CountCompletedByDriverIdAsync(driverId, cancellationToken);
        var upcomingTrip = await tripService.GetNextUpcomingAsync(
            driverId,
            utcNow,
            cancellationToken);
        var recentTrips = await tripService.GetLatestCompletedAsync(
            driverId,
            RecentTripCount,
            cancellationToken);

        var pendingBookingRequestCount = await unitOfWork.TripBookings
            .CountPendingByDriverIdAsync(driverId, cancellationToken);

        return new DriverDashboardResponseDto
        {
            DriverProfile = driverProfile,
            WalletBalance = walletBalance,
            TodayEarnings = todayEarnings,
            ThisWeekEarnings = thisWeekEarnings,
            TotalEarnings = totalEarnings,
            CompletedTripCount = completedTripCount,
            UpcomingTrip = upcomingTrip,
            RecentTrips = recentTrips,
            PendingBookingRequestCount = pendingBookingRequestCount
        };
    }
}
