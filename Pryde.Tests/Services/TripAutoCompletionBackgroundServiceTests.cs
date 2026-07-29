using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.BackgroundServices;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripAutoCompletionBackgroundServiceTests
{
    [Fact]
    public async Task ExpiredDeadlineAutomaticallyCompletesTrip()
    {
        var unitOfWork = new TestUnitOfWork();
        var context = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-1));
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();

        Assert.Equal(TripStatus.Completed, context.Trip.Status);
        Assert.True(context.Trip.WasAutoCompleted);
        Assert.NotNull(context.Trip.AutoCompletedAt);
        Assert.Equal(EscrowStatus.Released, context.Escrow.Status);
        Assert.Equal(100m, context.DriverWallet.Balance);
        Assert.Equal(BookingStatus.Completed, context.Booking.Status);
    }

    [Fact]
    public async Task FutureDeadlineIsSkipped()
    {
        var unitOfWork = new TestUnitOfWork();
        var context = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(1));
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();

        Assert.Equal(
            TripStatus.DropoffConfirmationPending,
            context.Trip.Status);
        Assert.False(context.Trip.WasAutoCompleted);
        Assert.Equal(EscrowStatus.Held, context.Escrow.Status);
        Assert.Equal(0m, context.DriverWallet.Balance);
    }

    [Fact]
    public async Task TripWithoutDriverEndTimestampIsSkipped()
    {
        var unitOfWork = new TestUnitOfWork();
        var context = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-1));
        context.Trip.DriverEndedAt = null;
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();

        Assert.Equal(
            TripStatus.DropoffConfirmationPending,
            context.Trip.Status);
        Assert.Equal(EscrowStatus.Held, context.Escrow.Status);
        Assert.Equal(0m, context.DriverWallet.Balance);
    }

    [Fact]
    public async Task DuplicateExecutionDoesNotCreditDriverTwice()
    {
        var unitOfWork = new TestUnitOfWork();
        var context = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-1));
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();
        await service.ProcessEligibleTripsAsync();

        Assert.Equal(100m, context.DriverWallet.Balance);
        Assert.Single(
            unitOfWork.WalletTransactionRepository.Items);
        Assert.Single(
            unitOfWork.LedgerRepository.Transactions);
    }

    [Theory]
    [InlineData(EscrowStatus.Released)]
    [InlineData(EscrowStatus.Refunded)]
    public async Task NonHeldEscrowIsNotReleased(
        EscrowStatus escrowStatus)
    {
        var unitOfWork = new TestUnitOfWork();
        var context = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-1));
        context.Escrow.Status = escrowStatus;
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();

        Assert.Equal(escrowStatus, context.Escrow.Status);
        Assert.Equal(0m, context.DriverWallet.Balance);
        Assert.Empty(
            unitOfWork.WalletTransactionRepository.Items);
        Assert.Empty(
            unitOfWork.LedgerRepository.Transactions);
    }

    [Fact]
    public async Task FailedTripDoesNotStopRemainingEligibleTrips()
    {
        var unitOfWork = new TestUnitOfWork();
        var failed = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-2),
            false);
        var successful = AddTrip(
            unitOfWork,
            DateTime.UtcNow.AddMinutes(-1));
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessEligibleTripsAsync();

        Assert.Equal(
            TripStatus.DropoffConfirmationPending,
            failed.Trip.Status);
        Assert.Equal(
            TripStatus.Completed,
            successful.Trip.Status);
        Assert.Equal(100m, successful.DriverWallet.Balance);
    }

    private static ServiceProvider Services(
        TestUnitOfWork unitOfWork)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddScoped<IFinancialService>(
            _ => new FinancialService(unitOfWork));
        return services.BuildServiceProvider();
    }

    private static TripAutoCompletionBackgroundService
        BackgroundService(ServiceProvider provider)
    {
        return new TripAutoCompletionBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TripAutoCompletionBackgroundService>.Instance);
    }

    private static AutoCompletionContext AddTrip(
        TestUnitOfWork unitOfWork,
        DateTime confirmationDeadline,
        bool addDriverWallet = true)
    {
        var driverId = Guid.NewGuid();
        var passengerId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            UserId = driverId,
            Capacity = 4,
            IsActive = true
        };
        var trip = new Trip
        {
            DriverId = driverId,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            DepartureTime = DateTime.UtcNow.AddHours(-2),
            Status = TripStatus.DropoffConfirmationPending,
            DriverEndedAt = confirmationDeadline.AddHours(-24),
            ConfirmationDeadline = confirmationDeadline
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = passengerId,
            Status = BookingStatus.Approved,
            PaidAt = DateTime.UtcNow.AddHours(-3),
            SeatPrice = 100m,
            ServiceCharge = 10m,
            TotalAmount = 110m
        };
        trip.Bookings.Add(booking);
        var escrow = new Escrow
        {
            BookingId = booking.Id,
            Booking = booking,
            PassengerId = passengerId,
            DriverId = driverId,
            Amount = 110m,
            DriverAmount = 100m,
            PlatformAmount = 10m,
            Status = EscrowStatus.Held,
            HeldAt = DateTime.UtcNow.AddHours(-3)
        };
        booking.Escrow = escrow;
        var passengerWallet = new Wallet
        {
            UserId = passengerId,
            EscrowBalance = 110m
        };
        var driverWallet = new Wallet
        {
            UserId = driverId
        };

        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        unitOfWork.EscrowRepository.Items.Add(escrow);
        unitOfWork.WalletRepository.Items.Add(passengerWallet);

        if (addDriverWallet)
        {
            unitOfWork.WalletRepository.Items.Add(driverWallet);
        }

        return new AutoCompletionContext(
            trip,
            booking,
            escrow,
            driverWallet);
    }

    private sealed record AutoCompletionContext(
        Trip Trip,
        TripBooking Booking,
        Escrow Escrow,
        Wallet DriverWallet);
}
