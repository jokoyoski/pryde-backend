using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripServiceTests
{
    [Fact]
    public async Task DriverCanCreateAValidTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var service = TestData.CreateTripService(unitOfWork);

        var result = await service.CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id));

        Assert.Equal(driverId, result.DriverId);
        Assert.Equal(TripStatus.Scheduled, result.Status);
        Assert.Single(unitOfWork.TripRepository.Items);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(false, VehicleOnboardingStatus.Approved)]
    [InlineData(true, VehicleOnboardingStatus.Draft)]
    [InlineData(true, VehicleOnboardingStatus.PendingReview)]
    [InlineData(true, VehicleOnboardingStatus.Rejected)]
    public async Task TripCreationRequiresActiveApprovedVehicle(
        bool isActive,
        VehicleOnboardingStatus onboardingStatus)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        vehicle.IsActive = isActive;
        vehicle.OnboardingStatus = onboardingStatus;

        await Assert.ThrowsAsync<ConflictException>(() =>
            TestData.CreateTripService(unitOfWork).CreateAsync(
                driverId,
                TestData.ValidTripRequest(vehicle.Id)));
    }

    [Fact]
    public async Task ApprovedActiveVehicleCanCreateTrip()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();

        var result = await TestData.CreateTripService(unitOfWork)
            .CreateAsync(
                driverId,
                TestData.ValidTripRequest(vehicle.Id));

        Assert.Equal(TripStatus.Scheduled, result.Status);
    }

    [Fact]
    public async Task DriverCannotCreateTripWithAnotherDriversVehicle()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        vehicle.UserId = Guid.NewGuid();
        var service = TestData.CreateTripService(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id)));
    }

    [Fact]
    public async Task AvailableSeatsCannotExceedVehicleCapacity()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);
        var request = TestData.ValidTripRequest(vehicle.Id);
        request.AvailableSeats = 5;

        await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateTripService(unitOfWork).CreateAsync(driverId, request));
    }

    [Fact]
    public async Task FareAndSeatPriceUseConfiguredValuesAndFullVehicleCapacity()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);

        var result = await TestData.CreateTripService(unitOfWork)
            .CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id));

        Assert.Equal(9500m, result.TripFare);
        Assert.Equal(2375m, result.SeatPrice);
        Assert.Equal(118.75m, result.PassengerServiceCharge);
        Assert.Equal(2493.75m, result.PassengerTotal);
    }

    [Fact]
    public async Task DriverCanCancelTripBeforeItStarts()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle, 1);
        var booking = TestData.Booking(trip, Guid.NewGuid(), BookingStatus.Approved);
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);

        await TestData.CreateTripService(unitOfWork).CancelAsync(trip.Id, driverId);

        Assert.Equal(TripStatus.Cancelled, trip.Status);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(2, trip.AvailableSeats);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task SearchExcludesCancelledCompletedDepartedFullAndBookingClosedTrips()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var valid = TestData.OpenTrip(driverId, vehicle);
        var cancelled = TestData.OpenTrip(driverId, vehicle); cancelled.Status = TripStatus.Cancelled;
        var completed = TestData.OpenTrip(driverId, vehicle); completed.Status = TripStatus.Completed;
        var departed = TestData.OpenTrip(driverId, vehicle); departed.DepartureTime = DateTime.UtcNow.AddMinutes(-1);
        var full = TestData.OpenTrip(driverId, vehicle, 0);
        var bookingClosed = TestData.OpenTrip(driverId, vehicle); bookingClosed.DepartureTime = DateTime.UtcNow.AddHours(4);
        unitOfWork.TripRepository.Items.AddRange([valid, cancelled, completed, departed, full, bookingClosed]);

        var results = await TestData.CreateTripService(unitOfWork).SearchAsync(new SearchTripsRequestDto());

        var result = Assert.Single(results);
        Assert.Equal(valid.Id, result.TripId);
    }

    [Fact]
    public async Task DriverStartsTripSuccessfully()
    {
        var context = CreateLifecycleContext();

        var response = await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);

        Assert.Equal(
            TripStatus.PickupConfirmationPending,
            response.Status);
        Assert.Equal(
            TripStatus.PickupConfirmationPending,
            context.Trip.Status);
        Assert.Equal(
            WorkflowNextAction.PassengerConfirmPickup,
            response.NextAction);
        Assert.Equal(
            WorkflowActor.Passenger,
            response.RequiredActor);
    }

    [Fact]
    public async Task NonDriverCannotStartTrip()
    {
        var context = CreateLifecycleContext();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.StartAsync(
                context.Trip.Id,
                Guid.NewGuid()));

        Assert.Equal(TripStatus.Scheduled, context.Trip.Status);
    }

    [Fact]
    public async Task PassengerConfirmsPickup()
    {
        var context = CreateLifecycleContext();
        await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);

        var response = await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        Assert.True(context.Bookings[0].PickupConfirmed);
        Assert.Equal(
            TripStatus.PickupConfirmationPending,
            context.Trip.Status);
        Assert.Equal(
            WorkflowNextAction.PassengerConfirmPickup,
            response.NextAction);
        Assert.Equal(
            WorkflowActor.Passenger,
            response.RequiredActor);
    }

    [Fact]
    public async Task DuplicatePickupConfirmationFails()
    {
        var context = CreateLifecycleContext();
        await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);
        await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ConfirmPickupAsync(
                context.Trip.Id,
                context.Bookings[0].PassengerId));
    }

    [Fact]
    public async Task TripMovesToInProgressAfterAllPickupConfirmations()
    {
        var context = CreateLifecycleContext();

        await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);
        await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);
        var response = await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[1].PassengerId);

        Assert.Equal(TripStatus.InProgress, context.Trip.Status);
        Assert.Equal(
            WorkflowNextAction.DriverEndTrip,
            response.NextAction);
        Assert.Equal(WorkflowActor.Driver, response.RequiredActor);
        Assert.All(
            context.Bookings,
            booking => Assert.True(booking.PickupConfirmed));
    }

    [Fact]
    public async Task DriverEndsTrip()
    {
        var context = CreateLifecycleContext();
        await MoveToInProgressAsync(context);

        var response = await context.Service.EndAsync(
            context.Trip.Id,
            context.DriverId);

        Assert.Equal(
            TripStatus.DropoffConfirmationPending,
            response.Status);
        Assert.Equal(
            WorkflowNextAction.PassengerConfirmDropoff,
            response.NextAction);
        Assert.Equal(
            WorkflowActor.Passenger,
            response.RequiredActor);
        Assert.All(
            context.Escrows,
            escrow => Assert.Equal(
                EscrowStatus.Held,
                escrow.Status));
    }

    [Fact]
    public async Task PassengerConfirmsDropoff()
    {
        var context = CreateLifecycleContext();
        await MoveToDropoffConfirmationAsync(context);

        var response = await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        Assert.True(context.Bookings[0].DropoffConfirmed);
        Assert.Equal(
            TripStatus.DropoffConfirmationPending,
            context.Trip.Status);
        Assert.Equal(
            WorkflowNextAction.PassengerConfirmDropoff,
            response.NextAction);
        Assert.Equal(
            WorkflowActor.Passenger,
            response.RequiredActor);
    }

    [Fact]
    public async Task DuplicateDropoffConfirmationFails()
    {
        var context = CreateLifecycleContext();
        await MoveToDropoffConfirmationAsync(context);
        await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ConfirmDropoffAsync(
                context.Trip.Id,
                context.Bookings[0].PassengerId));
    }

    [Fact]
    public async Task EscrowIsNotReleasedBeforeAllDropoffConfirmations()
    {
        var context = CreateLifecycleContext();
        await MoveToDropoffConfirmationAsync(context);

        await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        Assert.All(
            context.Escrows,
            escrow => Assert.Equal(
                EscrowStatus.Held,
                escrow.Status));
        Assert.Equal(0m, context.DriverWallet.Balance);
        Assert.Empty(
            context.UnitOfWork.LedgerRepository.Transactions);
    }

    [Fact]
    public async Task FinalDropoffConfirmationReleasesEscrow()
    {
        var context = CreateLifecycleContext();
        await MoveToDropoffConfirmationAsync(context);
        await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        var response = await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[1].PassengerId);

        Assert.Equal(TripStatus.Completed, response.Status);
        Assert.Equal(
            WorkflowNextAction.SubmitReview,
            response.NextAction);
        Assert.Equal(
            WorkflowActor.Passenger,
            response.RequiredActor);
        Assert.Equal(TripStatus.Completed, context.Trip.Status);
        Assert.Equal(200m, context.DriverWallet.Balance);
        Assert.All(
            context.Escrows,
            escrow => Assert.Equal(
                EscrowStatus.Released,
                escrow.Status));
        Assert.All(
            context.Bookings,
            booking => Assert.Equal(
                BookingStatus.Completed,
                booking.Status));
        Assert.Equal(
            2,
            context.UnitOfWork.LedgerRepository.Transactions.Count);
    }

    [Fact]
    public async Task InvalidLifecycleTransitionsFail()
    {
        var context = CreateLifecycleContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ConfirmPickupAsync(
                context.Trip.Id,
                context.Bookings[0].PassengerId));
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.EndAsync(
                context.Trip.Id,
                context.DriverId));
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ConfirmDropoffAsync(
                context.Trip.Id,
                context.Bookings[0].PassengerId));

        context.Trip.Status = TripStatus.InProgress;

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.StartAsync(
                context.Trip.Id,
                context.DriverId));
    }

    private static async Task MoveToInProgressAsync(
        LifecycleTestContext context)
    {
        await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);

        foreach (var booking in context.Bookings)
        {
            await context.Service.ConfirmPickupAsync(
                context.Trip.Id,
                booking.PassengerId);
        }
    }

    private static async Task MoveToDropoffConfirmationAsync(
        LifecycleTestContext context)
    {
        await MoveToInProgressAsync(context);
        await context.Service.EndAsync(
            context.Trip.Id,
            context.DriverId);
    }

    private static LifecycleTestContext CreateLifecycleContext()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.DepartureTime = DateTime.UtcNow.AddMinutes(-30);
        unitOfWork.TripRepository.Items.Add(trip);

        var bookings = new List<TripBooking>();
        var escrows = new List<Escrow>();

        for (var index = 0; index < 2; index++)
        {
            var passengerId = Guid.NewGuid();
            var booking = TestData.Booking(
                trip,
                passengerId,
                BookingStatus.Approved);
            booking.PaidAt = DateTime.UtcNow.AddMinutes(-45);
            booking.SeatPrice = 100m;
            booking.ServiceCharge = 10m;
            booking.TotalAmount = 110m;
            unitOfWork.TripBookingRepository.Items.Add(booking);
            bookings.Add(booking);

            unitOfWork.WalletRepository.Items.Add(new Wallet
            {
                UserId = passengerId,
                EscrowBalance = 110m
            });

            var escrow = new Escrow
            {
                BookingId = booking.Id,
                Booking = booking,
                PassengerId = passengerId,
                DriverId = driverId,
                Amount = 110m,
                DriverAmount = 100m,
                PlatformAmount = 10m,
                Currency = "NGN",
                Status = EscrowStatus.Held,
                HeldAt = DateTime.UtcNow.AddMinutes(-40)
            };
            unitOfWork.EscrowRepository.Items.Add(escrow);
            escrows.Add(escrow);
        }

        var driverWallet = new Wallet
        {
            UserId = driverId
        };
        unitOfWork.WalletRepository.Items.Add(driverWallet);

        return new LifecycleTestContext(
            unitOfWork,
            TestData.CreateTripService(unitOfWork),
            driverId,
            trip,
            bookings,
            escrows,
            driverWallet);
    }

    private sealed record LifecycleTestContext(
        TestUnitOfWork UnitOfWork,
        TripService Service,
        Guid DriverId,
        Trip Trip,
        IReadOnlyList<TripBooking> Bookings,
        IReadOnlyList<Escrow> Escrows,
        Wallet DriverWallet);
}
