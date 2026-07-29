using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class FinancialServiceTests
{
    [Fact]
    public async Task HoldPaymentMovesWalletFundsAndPostsBalancedLedgerEntries()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);

        var result = await service.HoldBookingPaymentAsync(
            context.Passenger.Id, context.Booking.Id, "payment-1");

        Assert.Equal(EscrowStatus.Held, result.Status);
        Assert.Equal(500m, context.PassengerWallet.Balance);
        Assert.Equal(2500m, context.PassengerWallet.EscrowBalance);
        Assert.Equal(2, context.UnitOfWork.LedgerRepository.Entries.Count);
        AssertBalanced(context.UnitOfWork.LedgerRepository.Transactions.Single());
        Assert.NotNull(context.Booking.PaidAt);
    }

    [Fact]
    public async Task BookingPaymentReturnsNextTripAction()
    {
        var context = CreateContext();
        var service = new TripBookingService(
            context.UnitOfWork,
            new FinancialService(context.UnitOfWork));

        var result = await service.PayAsync(
            context.Booking.Id,
            context.Passenger.Id,
            "workflow-payment");

        Assert.Equal(EscrowStatus.Held, result.Status);
        Assert.Equal(context.Trip.Id, result.TripId);
        Assert.Equal(
            WorkflowNextAction.DriverStartTrip,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
    }

    [Fact]
    public async Task SameIdempotencyKeyDoesNotDebitTwice()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);

        var first = await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "same-key");
        var second = await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "same-key");

        Assert.Equal(first.EscrowId, second.EscrowId);
        Assert.Equal(500m, context.PassengerWallet.Balance);
        Assert.Single(context.UnitOfWork.LedgerRepository.Transactions);
    }

    [Fact]
    public async Task DifferentKeyCannotPayAnAlreadyPaidBooking()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "first-key");

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "second-key"));
    }

    [Fact]
    public async Task RefundRestoresPassengerWalletAndCannotPostTwice()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "refund-hold");

        await service.RefundBookingAsync(context.Booking.Id);
        await service.RefundBookingAsync(context.Booking.Id);

        Assert.Equal(3000m, context.PassengerWallet.Balance);
        Assert.Equal(0m, context.PassengerWallet.EscrowBalance);
        Assert.Equal(EscrowStatus.Refunded, context.UnitOfWork.EscrowRepository.Items.Single().Status);
        Assert.Equal(2, context.UnitOfWork.LedgerRepository.Transactions.Count);
        Assert.All(context.UnitOfWork.LedgerRepository.Transactions, AssertBalanced);
    }

    [Fact]
    public async Task CompletingTripReleasesDriverAndPlatformSharesOnce()
    {
        var context = CreateContext();
        context.Trip.DepartureTime = DateTime.UtcNow.AddMinutes(-30);
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "release-hold");
        context.Trip.Status =
            TripStatus.DropoffConfirmationPending;
        context.Booking.DropoffConfirmed = true;

        await service.CompleteTripAsync(context.Trip.Id, context.Driver.Id);
        await service.CompleteTripAsync(context.Trip.Id, context.Driver.Id);
        var summary = await service.GetSummaryAsync();

        Assert.Equal(2400m, context.DriverWallet.Balance);
        Assert.Equal(100m, summary.TotalPlatformEarnings);
        Assert.Equal(2400m, summary.TotalDriverPayouts);
        Assert.Equal(EscrowStatus.Released, context.UnitOfWork.EscrowRepository.Items.Single().Status);
        Assert.Equal(TripStatus.Completed, context.Trip.Status);
        Assert.Equal(BookingStatus.Completed, context.Booking.Status);
        Assert.Equal(2, context.UnitOfWork.LedgerRepository.Transactions.Count);
        Assert.All(context.UnitOfWork.LedgerRepository.Transactions, AssertBalanced);
    }

    [Fact]
    public async Task EscrowListingFiltersByStatusAndLedgerDetailIsBalanced()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "list-hold");

        var escrows = await service.GetEscrowsAsync(new AdminEscrowsRequestDto
        {
            Status = EscrowStatus.Held,
            PassengerId = context.Passenger.Id
        });
        var transaction = context.UnitOfWork.LedgerRepository.Transactions.Single();
        var detail = await service.GetTransactionAsync(transaction.Id);

        Assert.Single(escrows.Items);
        Assert.Equal(
            detail.Entries.Where(entry => entry.EntryType == LedgerEntryType.Debit).Sum(entry => entry.Amount),
            detail.Entries.Where(entry => entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount));
    }

    private static FinancialContext CreateContext()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = new User { Id = Guid.NewGuid(), Email = "driver@test.local", Profile = new Profile { FirstName = "Dora", LastName = "Driver" } };
        var passenger = new User { Id = Guid.NewGuid(), Email = "passenger@test.local", Profile = new Profile { FirstName = "Pat", LastName = "Passenger" } };
        var vehicle = new Vehicle { UserId = driver.Id, User = driver, Capacity = 4, IsActive = true };
        var trip = new Trip
        {
            DriverId = driver.Id,
            Driver = driver,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            DepartureTime = DateTime.UtcNow.AddHours(2),
            Status = TripStatus.Scheduled,
            SeatPrice = 2400m,
            ServiceChargePercentage = 4m
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = passenger.Id,
            Passenger = passenger,
            Status = BookingStatus.Approved,
            SeatPrice = 2400m,
            ServiceCharge = 100m,
            TotalAmount = 2500m,
            RequestedAt = DateTime.UtcNow.AddHours(-1)
        };
        trip.Bookings.Add(booking);
        var passengerWallet = new Wallet { UserId = passenger.Id, User = passenger, Balance = 3000m };
        var driverWallet = new Wallet { UserId = driver.Id, User = driver };
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        unitOfWork.WalletRepository.Items.AddRange([passengerWallet, driverWallet]);
        return new FinancialContext(unitOfWork, driver, passenger, trip, booking, driverWallet, passengerWallet);
    }

    private static void AssertBalanced(LedgerTransaction transaction)
    {
        Assert.Equal(
            transaction.Entries.Where(entry => entry.EntryType == LedgerEntryType.Debit).Sum(entry => entry.Amount),
            transaction.Entries.Where(entry => entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount));
    }

    private sealed record FinancialContext(
        TestUnitOfWork UnitOfWork,
        User Driver,
        User Passenger,
        Trip Trip,
        TripBooking Booking,
        Wallet DriverWallet,
        Wallet PassengerWallet);
}
