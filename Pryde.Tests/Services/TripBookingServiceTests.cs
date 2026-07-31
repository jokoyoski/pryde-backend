using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripBookingServiceTests
{
    [Fact]
    public async Task PassengerCanRequestAnOpenTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();

        var result = await new TripBookingService(unitOfWork).CreateAsync(passengerId, trip.Id);

        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(
            WorkflowNextAction.AwaitDriverDecision,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
        Assert.Equal(2375m, result.SeatPrice);
        Assert.Equal(118.75m, result.ServiceCharge);
        Assert.Equal(2493.75m, result.TotalAmount);
        Assert.Equal(2, trip.AvailableSeats);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(driverId, notification.UserId);
        Assert.Equal(NotificationType.BookingRequested, notification.Type);
    }

    [Fact]
    public async Task DriverCannotBookTheirOwnTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(driverId, trip.Id));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Approved)]
    public async Task DuplicatePendingOrApprovedBookingIsRejected(BookingStatus status)
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();
        unitOfWork.TripBookingRepository.Items.Add(TestData.Booking(trip, passengerId, status));

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(passengerId, trip.Id));
    }

    [Fact]
    public async Task BookingRequestAfterCutoffIsRejected()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        trip.DepartureTime = DateTime.UtcNow.AddHours(4);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(Guid.NewGuid(), trip.Id));
    }

    [Fact]
    public async Task DriverCanApprovePendingRequest()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        var result = await new TripBookingService(unitOfWork).ApproveAsync(booking.Id, driverId);

        Assert.Equal(BookingStatus.Approved, result.Status);
        Assert.Equal(
            WorkflowNextAction.PayForBooking,
            result.NextAction);
        Assert.Equal(WorkflowActor.Passenger, result.RequiredActor);
        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(
            result.ApprovedAt.Value.AddMinutes(15),
            result.PaymentExpiresAt);
        Assert.Equal(1, trip.AvailableSeats);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(booking.PassengerId, notification.UserId);
        Assert.Equal(NotificationType.BookingApproved, notification.Type);
    }

    [Fact]
    public async Task ApprovalUsesConfiguredPaymentWindow()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = AddOpenTrip(
            unitOfWork,
            driverId,
            vehicle);
        var booking = AddBooking(unitOfWork, trip);
        var service = new TripBookingService(
            unitOfWork,
            new FinancialService(unitOfWork),
            Options.Create(new BookingPaymentSettings
            {
                PaymentWindowMinutes = 7,
                ExpiryCheckIntervalMinutes = 1
            }));

        var result = await service.ApproveAsync(
            booking.Id,
            driverId);

        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(
            result.ApprovedAt.Value.AddMinutes(7),
            result.PaymentExpiresAt);
    }

    [Fact]
    public async Task ApprovalDecrementsAvailableSeatsExactlyOnce()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);
        var service = new TripBookingService(unitOfWork);

        await service.ApproveAsync(booking.Id, driverId);
        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(booking.Id, driverId));

        Assert.Equal(1, trip.AvailableSeats);
        Assert.Single(unitOfWork.NotificationRepository.Items);
    }

    [Fact]
    public async Task ApprovalFailsWhenNoSeatsRemain()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle, 0);
        var booking = AddBooking(unitOfWork, trip);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).ApproveAsync(booking.Id, driverId));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task UnrelatedDriverCannotApproveRequest()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new TripBookingService(unitOfWork).ApproveAsync(booking.Id, Guid.NewGuid()));
        Assert.Empty(unitOfWork.NotificationRepository.Items);
    }

    [Fact]
    public async Task DecliningDoesNotDecrementSeats()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        var result = await new TripBookingService(unitOfWork).DeclineAsync(booking.Id, driverId);

        Assert.Equal(BookingStatus.Declined, result.Status);
        Assert.Equal(WorkflowNextAction.None, result.NextAction);
        Assert.Equal(WorkflowActor.None, result.RequiredActor);
        Assert.Equal(2, trip.AvailableSeats);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(booking.PassengerId, notification.UserId);
        Assert.Equal(NotificationType.BookingDeclined, notification.Type);
    }

    [Fact]
    public async Task CancellingApprovedBookingRestoresOneSeat()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle, 1);
        var passengerId = Guid.NewGuid();
        var booking = AddBooking(unitOfWork, trip, passengerId, BookingStatus.Approved);

        var result = await new TripBookingService(unitOfWork).CancelAsync(booking.Id, passengerId);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal(2, trip.AvailableSeats);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(driverId, notification.UserId);
        Assert.Equal(NotificationType.BookingCancelled, notification.Type);
    }

    [Fact]
    public async Task PaidCancellationBeforeTripStartRefundsPassenger()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext(4);
        var trip = AddOpenTrip(
            unitOfWork,
            driverId,
            vehicle,
            1);
        var passengerId = Guid.NewGuid();
        var booking = AddBooking(
            unitOfWork,
            trip,
            passengerId,
            BookingStatus.Approved);
        booking.PaidAt = DateTime.UtcNow.AddMinutes(-10);
        booking.SeatPrice = 100m;
        booking.ServiceCharge = 10m;
        booking.TotalAmount = 110m;
        var passengerWallet = new Pryde.Domain.Entities.Wallet
        {
            UserId = passengerId,
            Balance = 50m,
            EscrowBalance = 110m
        };
        unitOfWork.WalletRepository.Items.Add(passengerWallet);
        var escrow = new Pryde.Domain.Entities.Escrow
        {
            BookingId = booking.Id,
            Booking = booking,
            PassengerId = passengerId,
            DriverId = driverId,
            Amount = 110m,
            DriverAmount = 100m,
            PlatformAmount = 10m,
            Status = EscrowStatus.Held,
            HeldAt = DateTime.UtcNow.AddMinutes(-10)
        };
        booking.Escrow = escrow;
        unitOfWork.EscrowRepository.Items.Add(escrow);
        var service = new TripBookingService(
            unitOfWork,
            new FinancialService(unitOfWork));

        var result = await service.CancelAsync(
            booking.Id,
            passengerId);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
        Assert.NotNull(escrow.RefundedAt);
        Assert.Equal(160m, passengerWallet.Balance);
        Assert.Equal(0m, passengerWallet.EscrowBalance);
        Assert.Equal(2, trip.AvailableSeats);
        Assert.Single(
            unitOfWork.WalletTransactionRepository.Items);
        var ledgerTransaction = Assert.Single(
            unitOfWork.LedgerRepository.Transactions);
        Assert.Equal(
            ledgerTransaction.Entries
                .Where(entry =>
                    entry.EntryType ==
                    LedgerEntryType.Debit)
                .Sum(entry => entry.Amount),
            ledgerTransaction.Entries
                .Where(entry =>
                    entry.EntryType ==
                    LedgerEntryType.Credit)
                .Sum(entry => entry.Amount));
    }

    [Theory]
    [InlineData(TripStatus.PickupConfirmationPending)]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.DropoffConfirmationPending)]
    public async Task CancellationAfterTripStartsIsRejected(
        TripStatus tripStatus)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        trip.Status = tripStatus;
        var passengerId = Guid.NewGuid();
        var booking = AddBooking(
            unitOfWork,
            trip,
            passengerId,
            BookingStatus.Approved);
        booking.PaidAt = DateTime.UtcNow.AddMinutes(-10);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => new TripBookingService(unitOfWork)
                .CancelAsync(booking.Id, passengerId));

        Assert.Equal(
            "The booking cannot be cancelled after the trip has started.",
            exception.Message);
        Assert.Equal(BookingStatus.Approved, booking.Status);
    }

    [Fact]
    public async Task PassengerCannotCancelAnotherPassengersBooking()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip, Guid.NewGuid(), BookingStatus.Approved);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new TripBookingService(unitOfWork).CancelAsync(booking.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task PassengerCanViewTheirBookings()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();
        AddBooking(unitOfWork, trip, passengerId);

        var result = await new TripBookingService(unitOfWork).GetMineAsync(passengerId);

        Assert.Single(result);
    }

    [Fact]
    public async Task DriverCanViewConfirmedPassengers()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        AddBooking(unitOfWork, trip, status: BookingStatus.Approved);

        var result = await new TripBookingService(unitOfWork).GetConfirmedPassengersAsync(trip.Id, driverId);

        Assert.Single(result);
        Assert.Equal(BookingStatus.Approved, result[0].Status);
    }

    private static Pryde.Domain.Entities.Trip AddOpenTrip(
        TestUnitOfWork unitOfWork,
        Guid driverId,
        Pryde.Domain.Entities.Vehicle vehicle,
        int availableSeats = 2)
    {
        var trip = TestData.OpenTrip(driverId, vehicle, availableSeats);
        unitOfWork.TripRepository.Items.Add(trip);
        return trip;
    }

    private static Pryde.Domain.Entities.TripBooking AddBooking(
        TestUnitOfWork unitOfWork,
        Pryde.Domain.Entities.Trip trip,
        Guid? passengerId = null,
        BookingStatus status = BookingStatus.Pending)
    {
        var booking = TestData.Booking(trip, passengerId ?? Guid.NewGuid(), status);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        return booking;
    }
}
