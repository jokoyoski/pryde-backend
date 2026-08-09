using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
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
        Assert.Equal(
            2,
            context.UnitOfWork.NotificationRepository.Items.Count);
        Assert.All(
            context.UnitOfWork.NotificationRepository.Items,
            notification => Assert.Equal(
                NotificationType.BookingPaymentSuccessful,
                notification.Type));
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
    public async Task RefundRestoresPassengerWalletAndRejectsSecondRefund()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(context.Passenger.Id, context.Booking.Id, "refund-hold");

        await service.RefundBookingAsync(context.Booking.Id);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RefundBookingAsync(context.Booking.Id));

        Assert.Equal(3000m, context.PassengerWallet.Balance);
        Assert.Equal(0m, context.PassengerWallet.EscrowBalance);
        Assert.Equal(EscrowStatus.Refunded, context.UnitOfWork.EscrowRepository.Items.Single().Status);
        Assert.Equal(2, context.UnitOfWork.LedgerRepository.Transactions.Count);
        Assert.All(context.UnitOfWork.LedgerRepository.Transactions, AssertBalanced);
    }

    [Fact]
    public async Task ReleasedEscrowCannotBeRefunded()
    {
        var context = CreateContext();
        var service = new FinancialService(context.UnitOfWork);
        await service.HoldBookingPaymentAsync(
            context.Passenger.Id,
            context.Booking.Id,
            "released-refund");
        var escrow = Assert.Single(
            context.UnitOfWork.EscrowRepository.Items);
        escrow.Status = EscrowStatus.Released;

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RefundBookingAsync(context.Booking.Id));

        Assert.Equal(500m, context.PassengerWallet.Balance);
        Assert.Equal(2500m, context.PassengerWallet.EscrowBalance);
        Assert.Single(context.UnitOfWork.LedgerRepository.Transactions);
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
        Assert.NotNull(context.Trip.CompletedAt);
        Assert.Equal(BookingStatus.Completed, context.Booking.Status);
        Assert.Equal(2, context.UnitOfWork.LedgerRepository.Transactions.Count);
        Assert.All(context.UnitOfWork.LedgerRepository.Transactions, AssertBalanced);
        Assert.Contains(
            context.UnitOfWork.NotificationRepository.Items,
            notification =>
                notification.UserId == context.Driver.Id &&
                notification.Type == NotificationType.TripCompleted);
        Assert.Contains(
            context.UnitOfWork.NotificationRepository.Items,
            notification =>
                notification.UserId == context.Passenger.Id &&
                notification.Type == NotificationType.TripCompleted);
        Assert.Single(
            context.UnitOfWork.NotificationRepository.Items,
            notification =>
                notification.Type == NotificationType.EscrowReleased);
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

    [Fact]
    public async Task ExpiredUnpaidBookingIsCancelledAndRestoresSeatOnce()
    {
        var context = CreateContext();
        context.Trip.AvailableSeats = 1;
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(-1);
        var service = new FinancialService(
            context.UnitOfWork);

        var first = await service
            .ExpireUnpaidApprovedBookingAsync(
                context.Booking.Id,
                DateTime.UtcNow);
        var second = await service
            .ExpireUnpaidApprovedBookingAsync(
                context.Booking.Id,
                DateTime.UtcNow);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(
            BookingStatus.Cancelled,
            context.Booking.Status);
        Assert.Equal(2, context.Trip.AvailableSeats);
    }

    [Fact]
    public async Task PaidBookingDoesNotExpire()
    {
        var context = CreateContext();
        context.Booking.PaidAt =
            DateTime.UtcNow.AddMinutes(-1);
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(-2);

        var expired = await new FinancialService(
                context.UnitOfWork)
            .ExpireUnpaidApprovedBookingAsync(
                context.Booking.Id,
                DateTime.UtcNow);

        Assert.False(expired);
        Assert.Equal(
            BookingStatus.Approved,
            context.Booking.Status);
    }

    [Fact]
    public async Task UnexpiredBookingDoesNotExpire()
    {
        var context = CreateContext();
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(1);

        var expired = await new FinancialService(
                context.UnitOfWork)
            .ExpireUnpaidApprovedBookingAsync(
                context.Booking.Id,
                DateTime.UtcNow);

        Assert.False(expired);
        Assert.Equal(
            BookingStatus.Approved,
            context.Booking.Status);
    }

    [Fact]
    public async Task PaymentAfterExpiryIsRejectedWithoutWalletDebit()
    {
        var context = CreateContext();
        context.Trip.AvailableSeats = 1;
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(-1);
        var originalBalance =
            context.PassengerWallet.Balance;

        var exception = await Assert.ThrowsAsync<
            ConflictException>(() =>
            new FinancialService(context.UnitOfWork)
                .HoldBookingPaymentAsync(
                    context.Passenger.Id,
                    context.Booking.Id,
                    "expired-payment"));

        Assert.Equal(
            "The booking payment window has expired.",
            exception.Message);
        Assert.Equal(
            originalBalance,
            context.PassengerWallet.Balance);
        Assert.Equal(0m, context.PassengerWallet.EscrowBalance);
        Assert.Empty(
            context.UnitOfWork.EscrowRepository.Items);
        Assert.Empty(
            context.UnitOfWork.LedgerRepository.Transactions);
        Assert.Equal(
            BookingStatus.Cancelled,
            context.Booking.Status);
        Assert.Equal(2, context.Trip.AvailableSeats);
    }

    [Fact]
    public async Task PaymentBeforeExpirySucceeds()
    {
        var context = CreateContext();
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(1);

        var result = await new FinancialService(
                context.UnitOfWork)
            .HoldBookingPaymentAsync(
                context.Passenger.Id,
                context.Booking.Id,
                "before-expiry");

        Assert.Equal(EscrowStatus.Held, result.Status);
        Assert.NotNull(context.Booking.PaidAt);
        Assert.Equal(500m, context.PassengerWallet.Balance);
    }

    [Fact]
    public async Task PaymentAndExpiryCannotBothSucceed()
    {
        var context = CreateContext();
        context.Trip.AvailableSeats = 1;
        context.Booking.PaymentExpiresAt =
            DateTime.UtcNow.AddMinutes(-1);
        var service = new FinancialService(
            context.UnitOfWork);

        var paymentTask = CaptureConflictAsync(() =>
            service.HoldBookingPaymentAsync(
                context.Passenger.Id,
                context.Booking.Id,
                "payment-expiry-race"));
        var expiryTask =
            service.ExpireUnpaidApprovedBookingAsync(
                context.Booking.Id,
                DateTime.UtcNow);

        var results = await Task.WhenAll(
            paymentTask,
            expiryTask);

        Assert.False(results[0]);
        Assert.True(
            results[1] ||
            context.Booking.Status ==
                BookingStatus.Cancelled);
        Assert.Null(context.Booking.PaidAt);
        Assert.Empty(
            context.UnitOfWork.EscrowRepository.Items);
        Assert.Equal(2, context.Trip.AvailableSeats);
    }

    [Fact]
    public async Task FailedPaystackTransferRestoresWalletOnlyOnce()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        var wallet = new Wallet
        {
            UserId = userId,
            Balance = 500m
        };
        var withdrawal = new WalletTransaction
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 500m,
            Type = WalletTransactionType.Withdrawal,
            Reference = "pryde-wd-webhook",
            Status = WalletTransactionStatus.Pending,
            Provider = "Paystack",
            Currency = "NGN"
        };
        unitOfWork.WalletRepository.Items.Add(wallet);
        unitOfWork.WalletTransactionRepository.Items.Add(withdrawal);
        var service = new FinancialService(unitOfWork);

        var first = await service.ProcessPaystackTransferStatusAsync(
            withdrawal.Reference,
            50000,
            "failed");
        var second = await service.ProcessPaystackTransferStatusAsync(
            withdrawal.Reference,
            50000,
            "failed");

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(1000m, wallet.Balance);
        Assert.Equal(WalletTransactionStatus.Failed, withdrawal.Status);
        var reversal = Assert.Single(
            unitOfWork.LedgerRepository.Transactions);
        Assert.Equal(
            LedgerTransactionType.DriverWithdrawalReversal,
            reversal.TransactionType);
        AssertBalanced(reversal);
        Assert.Equal(
            NotificationType.WithdrawalFailed,
            Assert.Single(unitOfWork.NotificationRepository.Items).Type);
    }

    [Fact]
    public async Task SuccessfulPaystackTransferCompletesExistingWithdrawal()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        var wallet = new Wallet
        {
            UserId = userId,
            Balance = 500m
        };
        var withdrawal = new WalletTransaction
        {
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = 500m,
            Type = WalletTransactionType.Withdrawal,
            Reference = "pryde-wd-success",
            Status = WalletTransactionStatus.Pending,
            Provider = "Paystack",
            Currency = "NGN"
        };
        unitOfWork.WalletRepository.Items.Add(wallet);
        unitOfWork.WalletTransactionRepository.Items.Add(withdrawal);

        var handled = await new FinancialService(unitOfWork)
            .ProcessPaystackTransferStatusAsync(
                withdrawal.Reference,
                50000,
                "success");

        Assert.True(handled);
        Assert.Equal(500m, wallet.Balance);
        Assert.Equal(
            WalletTransactionStatus.Successful,
            withdrawal.Status);
        Assert.NotNull(withdrawal.CompletedAt);
        Assert.Empty(unitOfWork.LedgerRepository.Transactions);
        Assert.Equal(
            NotificationType.WithdrawalSuccessful,
            Assert.Single(unitOfWork.NotificationRepository.Items).Type);
    }

    private static async Task<bool> CaptureConflictAsync(
        Func<Task<EscrowResponseDto>> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
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
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            ApprovedAt = DateTime.UtcNow.AddMinutes(-1),
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(14)
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
