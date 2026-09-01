using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PassengerSearchAndDetailsExposeRecurringIdentity(
        bool isRecurring)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.RecurringTripId = isRecurring ? Guid.NewGuid() : null;
        unitOfWork.TripRepository.Items.Add(trip);
        var service = TestData.CreateTripService(unitOfWork);

        var summary = Assert.Single(await service.SearchAsync(
            new SearchTripsRequestDto()));
        var details = await service.GetByIdAsync(trip.Id);

        Assert.Equal(isRecurring, summary.IsRecurring);
        Assert.Equal(trip.RecurringTripId, summary.RecurringTripId);
        Assert.Equal(isRecurring, details.IsRecurring);
        Assert.Equal(trip.RecurringTripId, details.RecurringTripId);
        Assert.Equal(148.75m, summary.PassengerServiceCharge);
        Assert.Equal(2523.75m, summary.PassengerTotal);
        Assert.Equal(148.75m, details.PassengerServiceCharge);
        Assert.Equal(2523.75m, details.PassengerTotal);
    }

    [Fact]
    public async Task CustomerTripDetailsIncludeSafeDriverAndVehicleSummaries()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        vehicle.Make = "Toyota";
        vehicle.Model = "Corolla";
        vehicle.ManufacturingYear = 2022;
        vehicle.Colour = "Blue";
        vehicle.Images.Add(new VehicleImage
        {
            ImageUrl = "https://example.test/vehicle-side.jpg"
        });
        vehicle.Images.Add(new VehicleImage
        {
            ImageUrl = "https://example.test/vehicle-primary.jpg",
            IsPrimary = true
        });
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.Driver.Profile!.ProfilePhotoUrl =
            "https://example.test/driver.jpg";
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripRatingRepository.Items.AddRange(
        [
            new TripRating { RatedUserId = driverId, Value = 4 },
            new TripRating { RatedUserId = driverId, Value = 5 }
        ]);

        var result = await TestData.CreateTripService(unitOfWork)
            .GetByIdAsync(trip.Id);

        Assert.Equal(trip.Id, result.TripId);
        Assert.Equal("Lagos Island", result.OriginAddress);
        Assert.Equal(driverId, result.Driver.Id);
        Assert.Equal("Ada Driver", result.Driver.FullName);
        Assert.Equal(
            "https://example.test/driver.jpg",
            result.Driver.ProfileImageUrl);
        Assert.Equal(4.5, result.Driver.AverageRating);
        Assert.Equal(vehicle.Id, result.Vehicle.Id);
        Assert.Equal("Toyota", result.Vehicle.Make);
        Assert.Equal("Corolla", result.Vehicle.Model);
        Assert.Equal(2022, result.Vehicle.Year);
        Assert.Equal("Blue", result.Vehicle.Color);
        Assert.Equal("PRYDE-01", result.Vehicle.PlateNumber);
        Assert.Equal(
            "https://example.test/vehicle-primary.jpg",
            result.Vehicle.PrimaryImageUrl);
        Assert.Equal(vehicle.Capacity, result.Vehicle.Capacity);
        Assert.DoesNotContain(
            typeof(DriverSummaryDto).GetProperties(),
            property => property.Name is "Email" or "PhoneNumber");
    }

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

    [Fact]
    public async Task TripCreationUsesConfiguredFifteenMinuteDefault()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var request = TestData.ValidTripRequest(vehicle.Id);
        request.BookingWindowMinutes = null;

        var result = await TestData.CreateTripService(unitOfWork)
            .CreateAsync(driverId, request);

        Assert.Equal(15, result.BookingWindowMinutes);
    }

    [Fact]
    public async Task DriverCanConfigureBookingWindowInMinutes()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var request = TestData.ValidTripRequest(vehicle.Id);
        request.BookingWindowMinutes = 45;

        var result = await TestData.CreateTripService(unitOfWork)
            .CreateAsync(driverId, request);

        Assert.Equal(45, result.BookingWindowMinutes);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(14, false)]
    public async Task TripCreationEnforcesBookingCutoffBoundary(
        int departureMinutesFromNow,
        bool succeeds)
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var request = TestData.ValidTripRequest(vehicle.Id);
        request.DepartureTime = DateTime.UtcNow
            .AddMinutes(departureMinutesFromNow);
        request.BookingWindowMinutes = 15;

        if (succeeds)
        {
            await TestData.CreateTripService(unitOfWork)
                .CreateAsync(driverId, request);
            Assert.Single(unitOfWork.TripRepository.Items);
            return;
        }

        await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateTripService(unitOfWork)
                .CreateAsync(driverId, request));
        Assert.Empty(unitOfWork.TripRepository.Items);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(14, false)]
    public async Task TripUpdateEnforcesBookingCutoffBoundary(
        int departureMinutesFromNow,
        bool succeeds)
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle);
        unitOfWork.TripRepository.Items.Add(trip);
        var request = ToUpdate(TestData.ValidTripRequest(vehicle.Id));
        request.DepartureTime = DateTime.UtcNow
            .AddMinutes(departureMinutesFromNow);

        if (succeeds)
        {
            var result = await TestData.CreateTripService(unitOfWork)
                .UpdateAsync(trip.Id, driverId, request);
            Assert.Equal(request.DepartureTime, result.DepartureTime);
            return;
        }

        await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateTripService(unitOfWork)
                .UpdateAsync(trip.Id, driverId, request));
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
        Assert.Equal(148.75m, result.PassengerServiceCharge);
        Assert.Equal(2523.75m, result.PassengerTotal);
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
        var bookingClosed = TestData.OpenTrip(driverId, vehicle);
        bookingClosed.DepartureTime = DateTime.UtcNow.AddMinutes(15);
        unitOfWork.TripRepository.Items.AddRange([valid, cancelled, completed, departed, full, bookingClosed]);

        var results = await TestData.CreateTripService(unitOfWork).SearchAsync(new SearchTripsRequestDto());

        var result = Assert.Single(results);
        Assert.Equal(valid.Id, result.TripId);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(14, false)]
    public async Task DiscoveryEnforcesBookingCutoffBoundary(
        int departureMinutesFromNow,
        bool isReturned)
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle);
        trip.DepartureTime = DateTime.UtcNow
            .AddMinutes(departureMinutesFromNow);
        trip.BookingWindowMinutes = 15;
        unitOfWork.TripRepository.Items.Add(trip);

        var results = await TestData.CreateTripService(unitOfWork)
            .SearchAsync(new SearchTripsRequestDto());

        Assert.Equal(isReturned, results.Any(item => item.TripId == trip.Id));
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
    public async Task NearbySearchUsesValidatedRequestRadiusOverride()
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

        Assert.Single(results);
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
        Assert.False(response.IsRecurring);
        Assert.Null(response.RecurringTripId);
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
        Assert.False(response.IsRecurring);
        Assert.Null(response.RecurringTripId);
    }

    [Fact]
    public async Task RecurringPassengerConfirmationResponsesExposeParentSchedule()
    {
        var context = CreateLifecycleContext();
        var recurringTripId = Guid.NewGuid();
        context.Trip.RecurringTripId = recurringTripId;
        await context.Service.StartAsync(
            context.Trip.Id,
            context.DriverId);

        var pickup = await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);
        await context.Service.ConfirmPickupAsync(
            context.Trip.Id,
            context.Bookings[1].PassengerId);
        await context.Service.EndAsync(
            context.Trip.Id,
            context.DriverId);
        var dropoff = await context.Service.ConfirmDropoffAsync(
            context.Trip.Id,
            context.Bookings[0].PassengerId);

        Assert.True(pickup.IsRecurring);
        Assert.Equal(recurringTripId, pickup.RecurringTripId);
        Assert.True(dropoff.IsRecurring);
        Assert.Equal(recurringTripId, dropoff.RecurringTripId);
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
        Assert.False(response.IsRecurring);
        Assert.Null(response.RecurringTripId);
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

    private static UpdateTripRequestDto ToUpdate(
        CreateTripRequestDto request) => new()
    {
        VehicleId = request.VehicleId,
        OriginLatitude = request.OriginLatitude,
        OriginLongitude = request.OriginLongitude,
        OriginAddress = request.OriginAddress,
        DestinationLatitude = request.DestinationLatitude,
        DestinationLongitude = request.DestinationLongitude,
        DestinationAddress = request.DestinationAddress,
        DistanceKm = request.DistanceKm,
        EstimatedDurationMinutes = request.EstimatedDurationMinutes,
        DepartureTime = request.DepartureTime,
        AvailableSeats = request.AvailableSeats,
        AllowLuggage = request.AllowLuggage,
        BookingWindowMinutes = request.BookingWindowMinutes,
        RoutePolyline = request.RoutePolyline
    };

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
