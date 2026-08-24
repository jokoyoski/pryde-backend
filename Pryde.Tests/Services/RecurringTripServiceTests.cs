using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class RecurringTripServiceTests
{
    [Fact]
    public async Task DriverCanCreateListViewAndUpdateOwnedSchedule()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);

        var listed = await context.Service.GetMineAsync(context.DriverId);
        var viewed = await context.Service.GetOwnedAsync(
            created.RecurringTripId, context.DriverId);
        var update = ToUpdate(context.Request);
        update.AllowLuggage = false;
        var updated = await context.Service.UpdateAsync(
            created.RecurringTripId, context.DriverId, update);

        Assert.Single(listed);
        Assert.Equal(created.RecurringTripId, viewed.RecurringTripId);
        Assert.False(updated.AllowLuggage);
    }

    [Fact]
    public async Task AnotherDriverCannotManageSchedule()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.PauseAsync(
                created.RecurringTripId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ScheduleRequiresAtLeastOneValidRecurringDay()
    {
        var context = CreateContext();
        context.Request.DaysOfWeek = RecurringDays.None;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId, context.Request));
    }

    [Fact]
    public async Task GeneratorCreatesOnlySelectedDaysAndIsIdempotent()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var now = DateTime.UtcNow;

        var firstCount = await context.Service.GenerateOccurrencesAsync(now);
        var secondCount = await context.Service.GenerateOccurrencesAsync(now);

        var trip = Assert.Single(context.UnitOfWork.TripRepository.Items);
        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Equal(created.RecurringTripId, trip.RecurringTripId);
        Assert.Equal(context.Request.StartDate, DateOnly.FromDateTime(trip.DepartureTime));
        Assert.Equal(context.Request.DepartureTime, TimeOnly.FromDateTime(trip.DepartureTime));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(14, false)]
    public async Task RecurringScheduleCreationEnforcesCutoffBoundary(
        int departureMinutesFromNow,
        bool succeeds)
    {
        var context = CreateContext();
        var departure = DateTime.UtcNow
            .AddMinutes(departureMinutesFromNow);
        context.Request.StartDate = DateOnly.FromDateTime(departure);
        context.Request.EndDate = context.Request.StartDate;
        context.Request.DaysOfWeek = ToRecurringDay(departure.DayOfWeek);
        context.Request.DepartureTime = TimeOnly.FromDateTime(departure);

        if (succeeds)
        {
            await context.Service.CreateAsync(
                context.DriverId,
                context.Request);
            Assert.Single(context.UnitOfWork.RecurringTripRepository.Items);
            return;
        }

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.DriverId,
                context.Request));
    }

    [Theory]
    [InlineData(16, 1)]
    [InlineData(15, 0)]
    [InlineData(14, 0)]
    public async Task GeneratorSkipsOccurrencesAtOrAfterCutoff(
        int departureMinutesFromNow,
        int expectedGeneratedCount)
    {
        var context = CreateContext();
        await context.Service.CreateAsync(context.DriverId, context.Request);
        var schedule = context.UnitOfWork.RecurringTripRepository.Items.Single();
        var utcNow = DateTime.UtcNow;
        var departure = utcNow.AddMinutes(departureMinutesFromNow);
        schedule.StartDate = DateOnly.FromDateTime(departure);
        schedule.EndDate = schedule.StartDate;
        schedule.DaysOfWeek = ToRecurringDay(departure.DayOfWeek);
        schedule.DepartureTime = TimeOnly.FromDateTime(departure);
        schedule.BookingWindowMinutes = 15;

        var generated = await context.Service.GenerateOccurrencesAsync(utcNow);

        Assert.Equal(expectedGeneratedCount, generated);
        Assert.Equal(
            expectedGeneratedCount,
            context.UnitOfWork.TripRepository.Items.Count);
    }

    [Fact]
    public async Task PausedOrCancelledScheduleDoesNotGenerateFutureTrips()
    {
        var paused = CreateContext();
        var pausedSchedule = await paused.Service.CreateAsync(
            paused.DriverId, paused.Request);
        await paused.Service.PauseAsync(
            pausedSchedule.RecurringTripId, paused.DriverId);

        var cancelled = CreateContext();
        var cancelledSchedule = await cancelled.Service.CreateAsync(
            cancelled.DriverId, cancelled.Request);
        await cancelled.Service.CancelAsync(
            cancelledSchedule.RecurringTripId, cancelled.DriverId);

        Assert.Equal(0, await paused.Service.GenerateOccurrencesAsync(DateTime.UtcNow));
        Assert.Equal(0, await cancelled.Service.GenerateOccurrencesAsync(DateTime.UtcNow));
    }

    [Fact]
    public async Task ScheduleCanResumeButCancelledScheduleCannotResume()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);

        var paused = await context.Service.PauseAsync(
            created.RecurringTripId, context.DriverId);
        var resumed = await context.Service.ResumeAsync(
            created.RecurringTripId, context.DriverId);
        var cancelled = await context.Service.CancelAsync(
            created.RecurringTripId, context.DriverId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ResumeAsync(
                created.RecurringTripId, context.DriverId));

        Assert.False(paused.IsActive);
        Assert.True(resumed.IsActive);
        Assert.True(cancelled.IsCancelled);
    }

    [Fact]
    public async Task SubscriptionPreventsDuplicatesAndSupportsCancelAndResubscribe()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var passengerId = AddPassenger(context.UnitOfWork);

        var subscription = await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.SubscribeAsync(
                created.RecurringTripId, passengerId));
        var cancelled = await context.Service.CancelSubscriptionAsync(
            created.RecurringTripId, passengerId);
        var reactivated = await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);

        Assert.True(subscription.IsActive);
        Assert.False(cancelled.IsActive);
        Assert.True(reactivated.IsActive);
        Assert.Equal(subscription.SubscriptionId, reactivated.SubscriptionId);
        Assert.Single(context.UnitOfWork.TripSubscriptionRepository.Items);
    }

    [Fact]
    public async Task SubscriptionCannotExceedScheduleSeatCapacity()
    {
        var context = CreateContext(capacity: 1);
        context.Request.AvailableSeats = 1;
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, AddPassenger(context.UnitOfWork));

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.SubscribeAsync(
                created.RecurringTripId,
                AddPassenger(context.UnitOfWork)));
    }

    [Fact]
    public async Task GeneratorCreatesPendingBookingForEveryActiveSubscriber()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var firstPassengerId = AddPassenger(context.UnitOfWork);
        var secondPassengerId = AddPassenger(context.UnitOfWork);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, firstPassengerId);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, secondPassengerId);

        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);

        var trip = Assert.Single(context.UnitOfWork.TripRepository.Items);
        var bookings = context.UnitOfWork.TripBookingRepository.Items;
        Assert.Equal(2, bookings.Count);
        Assert.All(bookings, booking =>
        {
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Equal(trip.SeatPrice, booking.SeatPrice);
            Assert.Equal(
                trip.SeatPrice + booking.ServiceCharge,
                booking.TotalAmount);
        });
        Assert.Equal(context.Request.AvailableSeats, trip.AvailableSeats);
        Assert.Equal(2, context.UnitOfWork.NotificationRepository.Items.Count);
    }

    [Fact]
    public async Task RepeatedGenerationDoesNotCreateDuplicateSubscriberBooking()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var passengerId = AddPassenger(context.UnitOfWork);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);

        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);

        Assert.Single(context.UnitOfWork.TripRepository.Items);
        Assert.Single(context.UnitOfWork.TripBookingRepository.Items);
        Assert.Single(context.UnitOfWork.NotificationRepository.Items);
    }

    [Fact]
    public async Task CancellingSubscriptionPreservesExistingBookingAndStopsFutureRequests()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var passengerId = AddPassenger(context.UnitOfWork);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);
        var existingBooking = Assert.Single(
            context.UnitOfWork.TripBookingRepository.Items);

        await context.Service.CancelSubscriptionAsync(
            created.RecurringTripId, passengerId);
        await CreateExistingOccurrenceAsync(
            context,
            created.RecurringTripId,
            DateTime.UtcNow.AddDays(2));
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);

        Assert.Single(context.UnitOfWork.TripBookingRepository.Items);
        Assert.Equal(BookingStatus.Pending, existingBooking.Status);
    }

    [Fact]
    public async Task GeneratorBackfillsOpenExistingFutureOccurrence()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var departure = ToUtc(
            context.Request.StartDate,
            context.Request.DepartureTime);
        var occurrence = await CreateExistingOccurrenceAsync(
            context, created.RecurringTripId, departure);
        var passengerId = AddPassenger(context.UnitOfWork);
        await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);

        var generated = await context.Service.GenerateOccurrencesAsync(
            DateTime.UtcNow);

        Assert.Equal(0, generated);
        var booking = Assert.Single(
            context.UnitOfWork.TripBookingRepository.Items);
        Assert.Equal(occurrence.TripId, booking.TripId);
        Assert.Equal(passengerId, booking.PassengerId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task BackfillSkipsPastClosedAndCancelledOccurrences()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var schedule = context.UnitOfWork.RecurringTripRepository.Items.Single();
        schedule.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        schedule.EndDate = schedule.StartDate;
        await context.Service.SubscribeAsync(
            created.RecurringTripId,
            AddPassenger(context.UnitOfWork));

        var past = TestData.OpenTrip(context.DriverId, context.Vehicle);
        past.RecurringTripId = created.RecurringTripId;
        past.DepartureTime = DateTime.UtcNow.AddMinutes(-1);
        var closed = TestData.OpenTrip(context.DriverId, context.Vehicle);
        closed.RecurringTripId = created.RecurringTripId;
        closed.DepartureTime = DateTime.UtcNow.AddMinutes(10);
        closed.BookingWindowMinutes = 15;
        var cancelled = TestData.OpenTrip(context.DriverId, context.Vehicle);
        cancelled.RecurringTripId = created.RecurringTripId;
        cancelled.Status = TripStatus.Cancelled;
        context.UnitOfWork.TripRepository.Items.AddRange(
            [past, closed, cancelled]);

        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);

        Assert.Empty(context.UnitOfWork.TripBookingRepository.Items);
    }

    [Fact]
    public async Task CapacityUpdateAndSubscriptionCannotOversubscribeSchedule()
    {
        var context = CreateContext(capacity: 2);
        context.Request.AvailableSeats = 2;
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        await context.Service.SubscribeAsync(
            created.RecurringTripId,
            AddPassenger(context.UnitOfWork));
        var secondPassengerId = AddPassenger(context.UnitOfWork);
        var update = ToUpdate(context.Request);
        update.AvailableSeats = 1;

        var results = await Task.WhenAll(
            CaptureExceptionAsync(() => context.Service.SubscribeAsync(
                created.RecurringTripId, secondPassengerId)),
            CaptureExceptionAsync(() => context.Service.UpdateAsync(
                created.RecurringTripId, context.DriverId, update)));

        Assert.Single(results, exception => exception is ConflictException);
        var schedule = context.UnitOfWork.RecurringTripRepository.Items.Single();
        var activeCount = context.UnitOfWork.TripSubscriptionRepository.Items
            .Count(subscription => subscription.IsActive);
        Assert.True(activeCount <= schedule.AvailableSeats);
    }

    [Fact]
    public async Task GeneratedBookingUsesNormalApprovalAndPaymentFlow()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        var passengerId = AddPassenger(context.UnitOfWork);
        context.UnitOfWork.WalletRepository.Items.Add(new Wallet
        {
            UserId = passengerId,
            Balance = 100_000m
        });
        await context.Service.SubscribeAsync(
            created.RecurringTripId, passengerId);
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);
        var trip = Assert.Single(context.UnitOfWork.TripRepository.Items);
        var booking = Assert.Single(
            context.UnitOfWork.TripBookingRepository.Items);
        var initialSeats = trip.AvailableSeats;

        var approved = await context.TripBookingService.ApproveAsync(
            booking.Id, context.DriverId);
        var payment = await context.TripBookingService.PayAsync(
            booking.Id, passengerId, "recurring-booking-payment");

        Assert.Equal(BookingStatus.Approved, approved.Status);
        Assert.Equal(initialSeats - 1, trip.AvailableSeats);
        Assert.Equal(EscrowStatus.Held, payment.Status);
        Assert.NotNull(booking.PaidAt);
    }

    [Fact]
    public async Task CancellingGeneratedOccurrenceDoesNotCancelSchedule()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);
        var trip = Assert.Single(context.UnitOfWork.TripRepository.Items);

        await context.TripService.CancelAsync(trip.Id, context.DriverId);
        var schedule = await context.Service.GetOwnedAsync(
            created.RecurringTripId, context.DriverId);

        Assert.Equal(TripStatus.Cancelled, trip.Status);
        Assert.True(schedule.IsActive);
        Assert.False(schedule.IsCancelled);
    }

    [Fact]
    public async Task AdminCanSeeSchedulesAndGeneratedTrips()
    {
        var context = CreateContext();
        var created = await context.Service.CreateAsync(
            context.DriverId, context.Request);
        await context.Service.GenerateOccurrencesAsync(DateTime.UtcNow);

        var page = await context.Service.AdminGetAllAsync(
            new AdminRecurringTripsRequestDto());
        var details = await context.Service.AdminGetByIdAsync(
            created.RecurringTripId);

        Assert.Single(page.Items);
        Assert.Single(details.GeneratedTrips);
    }

    [Fact]
    public async Task OneTimeTripCreationRemainsNonRecurring()
    {
        var context = CreateContext();

        await context.TripService.CreateAsync(
            context.DriverId,
            TestData.ValidTripRequest(context.Vehicle.Id));

        Assert.Null(Assert.Single(
            context.UnitOfWork.TripRepository.Items).RecurringTripId);
    }

    private static RecurringTestContext CreateContext(int capacity = 4)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext(capacity);
        var tripService = TestData.CreateTripService(unitOfWork);
        var tripBookingService = new TripBookingService(
            unitOfWork,
            new FinancialService(
                unitOfWork,
                Options.Create(new PricingSettings
                {
                    PlatformSharePercent = 30m
                })));
        var service = new RecurringTripService(
            unitOfWork,
            tripService,
            tripBookingService,
            Options.Create(new RecurringTripSettings
            {
                GenerationHorizonDays = 14,
                GenerationIntervalMinutes = 15
            }),
            Options.Create(new TripSettings()));
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        return new RecurringTestContext(
            unitOfWork,
            driverId,
            vehicle,
            tripService,
            tripBookingService,
            service,
            new CreateRecurringTripRequestDto
            {
                VehicleId = vehicle.Id,
                OriginLatitude = 6.5244,
                OriginLongitude = 3.3792,
                OriginAddress = "Lagos Island",
                DestinationLatitude = 6.6018,
                DestinationLongitude = 3.3515,
                DestinationAddress = "Ikeja",
                DistanceKm = 10,
                EstimatedDurationMinutes = 20,
                AvailableSeats = Math.Min(3, capacity),
                AllowLuggage = true,
                BookingWindowMinutes = 15,
                StartDate = startDate,
                EndDate = startDate,
                DaysOfWeek = ToRecurringDay(startDate.DayOfWeek),
                DepartureTime = new TimeOnly(12, 0)
            });
    }

    private static Guid AddPassenger(TestUnitOfWork unitOfWork)
    {
        var passenger = new User { Id = Guid.NewGuid() };
        unitOfWork.UserRepository.Items.Add(passenger);
        return passenger.Id;
    }

    private static async Task<Pryde.Contracts.ResponseModels.TripDetailsResponseDto>
        CreateExistingOccurrenceAsync(
            RecurringTestContext context,
            Guid recurringTripId,
            DateTime departureTime)
    {
        var request = TestData.ValidTripRequest(context.Vehicle.Id);
        request.DepartureTime = departureTime;
        request.AvailableSeats = context.Request.AvailableSeats;
        request.BookingWindowMinutes = context.Request.BookingWindowMinutes;
        return await context.TripService.CreateRecurringOccurrenceAsync(
            context.DriverId,
            recurringTripId,
            request);
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);

    private static async Task<Exception?> CaptureExceptionAsync(
        Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static UpdateRecurringTripRequestDto ToUpdate(
        CreateRecurringTripRequestDto request) => new()
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
            RoutePolyline = request.RoutePolyline,
            AvailableSeats = request.AvailableSeats,
            AllowLuggage = request.AllowLuggage,
            BookingWindowMinutes = request.BookingWindowMinutes,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DaysOfWeek = request.DaysOfWeek,
            DepartureTime = request.DepartureTime
        };

    private static RecurringDays ToRecurringDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => RecurringDays.Monday,
        DayOfWeek.Tuesday => RecurringDays.Tuesday,
        DayOfWeek.Wednesday => RecurringDays.Wednesday,
        DayOfWeek.Thursday => RecurringDays.Thursday,
        DayOfWeek.Friday => RecurringDays.Friday,
        DayOfWeek.Saturday => RecurringDays.Saturday,
        DayOfWeek.Sunday => RecurringDays.Sunday,
        _ => RecurringDays.None
    };

    private sealed record RecurringTestContext(
        TestUnitOfWork UnitOfWork,
        Guid DriverId,
        Vehicle Vehicle,
        TripService TripService,
        TripBookingService TripBookingService,
        RecurringTripService Service,
        CreateRecurringTripRequestDto Request);
}
