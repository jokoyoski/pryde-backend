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

    [Theory]
    [InlineData(KycStatus.Pending)]
    [InlineData(KycStatus.Rejected)]
    [InlineData(KycStatus.Submitted)]
    public async Task TripCreationRequiresApprovedKyc(KycStatus kycStatus)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        unitOfWork.KycVerificationRepository.Items.Single().Status =
            kycStatus;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            TestData.CreateTripService(unitOfWork).CreateAsync(
                driverId,
                TestData.ValidTripRequest(vehicle.Id)));

        Assert.Empty(unitOfWork.TripRepository.Items);
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
        Assert.Equal(2, unitOfWork.SaveChangesCount);
        var notification = Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(booking.PassengerId, notification.UserId);
        Assert.Equal(NotificationType.BookingCancelled, notification.Type);
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
    public async Task NearbySearchIncludesTripInsideConfiguredRadius()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = SearchTripAtDistance(driverId, vehicle, 1d);
        unitOfWork.TripRepository.Items.Add(trip);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto
            {
                Latitude = 0d,
                Longitude = 0d
            });

        Assert.Equal(trip.Id, Assert.Single(results).TripId);
    }

    [Fact]
    public async Task NearbySearchExcludesTripOutsideConfiguredRadius()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        unitOfWork.TripRepository.Items.Add(
            SearchTripAtDistance(driverId, vehicle, 2.1d));

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto
            {
                Latitude = 0d,
                Longitude = 0d,
                PickupRadiusKm = 100d
            });

        Assert.Empty(results);
    }

    [Fact]
    public async Task NearbySearchIncludesTripExactlyOnConfiguredBoundary()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = SearchTripAtDistance(
            driverId,
            vehicle,
            TestData.Pricing.PickupRadiusKm);
        unitOfWork.TripRepository.Items.Add(trip);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto
            {
                Latitude = 0d,
                Longitude = 0d
            });

        Assert.Equal(trip.Id, Assert.Single(results).TripId);
    }

    [Fact]
    public async Task SearchWithoutNearbyCoordinatesPreservesExistingBehavior()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var distantTrip = SearchTripAtDistance(
            driverId,
            vehicle,
            100d);
        unitOfWork.TripRepository.Items.Add(distantTrip);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto());

        Assert.Equal(distantTrip.Id, Assert.Single(results).TripId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchWithOnlyOneNearbyCoordinatePreservesExistingBehavior(
        bool supplyLatitude)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var distantTrip = SearchTripAtDistance(
            driverId,
            vehicle,
            100d);
        unitOfWork.TripRepository.Items.Add(distantTrip);
        var request = new SearchTripsRequestDto
        {
            Latitude = supplyLatitude ? 0d : null,
            Longitude = supplyLatitude ? null : 0d
        };

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(request);

        Assert.Equal(distantTrip.Id, Assert.Single(results).TripId);
    }

    [Fact]
    public async Task NearbySearchPreservesExistingDepartureTimeOrdering()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var later = SearchTripAtDistance(driverId, vehicle, 1d);
        later.DepartureTime = DateTime.UtcNow.AddHours(12);
        var earlier = SearchTripAtDistance(driverId, vehicle, 1d);
        earlier.DepartureTime = DateTime.UtcNow.AddHours(10);
        unitOfWork.TripRepository.Items.AddRange([later, earlier]);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto
            {
                Latitude = 0d,
                Longitude = 0d
            });

        Assert.Equal(
            [earlier.Id, later.Id],
            results.Select(result => result.TripId).ToList());
    }

    [Fact]
    public async Task NearbySearchPreservesExistingSeatAndLuggageFilters()
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var matching = SearchTripAtDistance(driverId, vehicle, 1d);
        matching.AllowLuggage = true;
        matching.AvailableSeats = 2;
        var noLuggage = SearchTripAtDistance(driverId, vehicle, 1d);
        noLuggage.AllowLuggage = false;
        var insufficientSeats = SearchTripAtDistance(
            driverId,
            vehicle,
            1d);
        insufficientSeats.AllowLuggage = true;
        insufficientSeats.AvailableSeats = 1;
        unitOfWork.TripRepository.Items.AddRange(
            [matching, noLuggage, insufficientSeats]);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto
            {
                Latitude = 0d,
                Longitude = 0d,
                RequiresLuggage = true,
                RequiredSeats = 2
            });

        Assert.Equal(matching.Id, Assert.Single(results).TripId);
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
        var notifications = context.UnitOfWork.NotificationRepository.Items
            .Where(notification =>
                notification.Type == NotificationType.PickupConfirmationRequired)
            .ToList();
        Assert.Equal(2, notifications.Count);
        Assert.All(
            context.Bookings,
            booking => Assert.Contains(
                notifications,
                notification => notification.UserId == booking.PassengerId));
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
        Assert.NotNull(context.Trip.DriverEndedAt);
        Assert.NotNull(context.Trip.ConfirmationDeadline);
        Assert.Equal(
            TimeSpan.FromHours(24),
            context.Trip.ConfirmationDeadline.Value -
            context.Trip.DriverEndedAt.Value);
        Assert.All(
            context.Escrows,
            escrow => Assert.Equal(
                EscrowStatus.Held,
                escrow.Status));
        var notifications = context.UnitOfWork.NotificationRepository.Items
            .Where(notification =>
                notification.Type == NotificationType.DropoffConfirmationRequired)
            .ToList();
        Assert.Equal(2, notifications.Count);
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
        Assert.Equal(
            3,
            context.UnitOfWork.NotificationRepository.Items.Count(
                notification =>
                    notification.Type == NotificationType.TripCompleted));
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

    private static Trip SearchTripAtDistance(
        Guid driverId,
        Vehicle vehicle,
        double distanceKm)
    {
        const double earthRadiusKm = 6371d;
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.OriginLatitude =
            distanceKm / earthRadiusKm * 180d / Math.PI;
        trip.OriginLongitude = 0d;
        return trip;
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
            booking.Escrow = escrow;
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
