using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class DriverDashboardServiceTests
{
    [Fact]
    public async Task DashboardReturnsSuccessfulResponse()
    {
        var (unitOfWork, driverId, _) = TestData.CreateDriverContext();
        var wallet = AddWallet(unitOfWork, driverId, 15000m);
        AddEarning(unitOfWork, wallet, 2500m, DateTime.UtcNow);

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.Equal(driverId, response.DriverProfile.UserId);
        Assert.Equal(15000m, response.WalletBalance);
        Assert.Equal(2500m, response.TodayEarnings);
        Assert.Equal(2500m, response.TotalEarnings);
        Assert.Empty(response.RecentTrips);
        Assert.Null(response.UpcomingTrip);
        Assert.Equal(0, response.PendingBookingRequestCount);
    }

    [Fact]
    public async Task DriverWithNoTripsReturnsZeroValuesAndEmptyTrips()
    {
        var (unitOfWork, driverId, _) = TestData.CreateDriverContext();

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.Equal(0m, response.WalletBalance);
        Assert.Equal(0m, response.TodayEarnings);
        Assert.Equal(0m, response.TotalEarnings);
        Assert.Empty(response.RecentTrips);
        Assert.Null(response.UpcomingTrip);
        Assert.Equal(0, response.PendingBookingRequestCount);
    }

    [Fact]
    public async Task DriverWithPendingBookingRequestsReturnsPendingCount()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var driverTrip = TestData.OpenTrip(driverId, vehicle);
        var otherDriverTrip = TestData.OpenTrip(Guid.NewGuid(), vehicle);

        unitOfWork.TripRepository.Items.Add(driverTrip);
        unitOfWork.TripRepository.Items.Add(otherDriverTrip);
        unitOfWork.TripBookingRepository.Items.Add(
            TestData.Booking(driverTrip, Guid.NewGuid(), BookingStatus.Pending));
        unitOfWork.TripBookingRepository.Items.Add(
            TestData.Booking(driverTrip, Guid.NewGuid(), BookingStatus.Approved));
        unitOfWork.TripBookingRepository.Items.Add(
            TestData.Booking(otherDriverTrip, Guid.NewGuid(), BookingStatus.Pending));

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.Equal(1, response.PendingBookingRequestCount);
    }

    [Fact]
    public async Task DriverWithCompletedTripsReturnsRecentTripsAndEarnings()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var olderTrip = CompletedTrip(
            driverId,
            vehicle,
            DateTime.UtcNow.AddDays(-2));
        var recentTrip = CompletedTrip(
            driverId,
            vehicle,
            DateTime.UtcNow.AddDays(-1));
        var wallet = AddWallet(unitOfWork, driverId, 5000m);

        unitOfWork.TripRepository.Items.Add(olderTrip);
        unitOfWork.TripRepository.Items.Add(recentTrip);
        AddEarning(unitOfWork, wallet, 1200m, DateTime.UtcNow.AddDays(-1));
        AddEarning(unitOfWork, wallet, 1800m, DateTime.UtcNow);
        AddWalletTransaction(
            unitOfWork,
            wallet,
            900m,
            WalletTransactionType.Credit,
            DateTime.UtcNow);

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.Equal(2, response.RecentTrips.Count);
        Assert.Equal(recentTrip.Id, response.RecentTrips[0].TripId);
        Assert.Equal(1800m, response.TodayEarnings);
        Assert.Equal(3000m, response.TotalEarnings);
    }

    [Fact]
    public async Task DriverWithUpcomingTripsReturnsNearestFutureTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var laterTrip = TestData.OpenTrip(driverId, vehicle);
        laterTrip.DepartureTime = DateTime.UtcNow.AddDays(2);
        var nearestTrip = TestData.OpenTrip(driverId, vehicle);
        nearestTrip.DepartureTime = DateTime.UtcNow.AddHours(4);
        var cancelledTrip = TestData.OpenTrip(driverId, vehicle);
        cancelledTrip.DepartureTime = DateTime.UtcNow.AddHours(1);
        cancelledTrip.Status = TripStatus.Cancelled;

        unitOfWork.TripRepository.Items.Add(laterTrip);
        unitOfWork.TripRepository.Items.Add(nearestTrip);
        unitOfWork.TripRepository.Items.Add(cancelledTrip);

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.NotNull(response.UpcomingTrip);
        Assert.Equal(nearestTrip.Id, response.UpcomingTrip.TripId);
    }

    [Fact]
    public async Task DashboardReturnsOnlyAuthenticatedDriversData()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var otherDriverId = Guid.NewGuid();
        var ownCompletedTrip = CompletedTrip(
            driverId,
            vehicle,
            DateTime.UtcNow.AddDays(-1));
        var otherCompletedTrip = CompletedTrip(
            otherDriverId,
            vehicle,
            DateTime.UtcNow);
        var ownUpcomingTrip = TestData.OpenTrip(driverId, vehicle);
        var otherUpcomingTrip = TestData.OpenTrip(otherDriverId, vehicle);
        otherUpcomingTrip.DepartureTime = DateTime.UtcNow.AddHours(1);

        unitOfWork.TripRepository.Items.Add(ownCompletedTrip);
        unitOfWork.TripRepository.Items.Add(otherCompletedTrip);
        unitOfWork.TripRepository.Items.Add(ownUpcomingTrip);
        unitOfWork.TripRepository.Items.Add(otherUpcomingTrip);
        unitOfWork.TripBookingRepository.Items.Add(
            TestData.Booking(otherUpcomingTrip, Guid.NewGuid(), BookingStatus.Pending));

        var response = await CreateService(unitOfWork).GetAsync(driverId);

        Assert.Single(response.RecentTrips);
        Assert.Equal(ownCompletedTrip.Id, response.RecentTrips[0].TripId);
        Assert.NotNull(response.UpcomingTrip);
        Assert.Equal(ownUpcomingTrip.Id, response.UpcomingTrip.TripId);
        Assert.Equal(0, response.PendingBookingRequestCount);
    }

    private static DriverDashboardService CreateService(
        TestUnitOfWork unitOfWork)
    {
        return new DriverDashboardService(
            new ProfileService(unitOfWork),
            new WalletService(unitOfWork),
            TestData.CreateTripService(unitOfWork),
            unitOfWork);
    }

    private static Wallet AddWallet(
        TestUnitOfWork unitOfWork,
        Guid driverId,
        decimal balance)
    {
        var wallet = new Wallet
        {
            UserId = driverId,
            Balance = balance
        };

        unitOfWork.WalletRepository.Items.Add(wallet);
        return wallet;
    }

    private static void AddEarning(
        TestUnitOfWork unitOfWork,
        Wallet wallet,
        decimal amount,
        DateTime createdAt)
    {
        AddWalletTransaction(
            unitOfWork,
            wallet,
            amount,
            WalletTransactionType.EscrowRelease,
            createdAt);
    }

    private static void AddWalletTransaction(
        TestUnitOfWork unitOfWork,
        Wallet wallet,
        decimal amount,
        WalletTransactionType transactionType,
        DateTime createdAt)
    {
        unitOfWork.WalletTransactionRepository.Items.Add(
            new WalletTransaction
            {
                WalletId = wallet.Id,
                Wallet = wallet,
                Amount = amount,
                Type = transactionType,
                CreatedAt = createdAt
            });
    }

    private static Trip CompletedTrip(
        Guid driverId,
        Vehicle vehicle,
        DateTime departureTime)
    {
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.DepartureTime = departureTime;
        trip.Status = TripStatus.Completed;
        return trip;
    }
}
