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
        var schedule = context.UnitOfWork.RecurringTripRepository.Items.Single();
        schedule.Trips.Add(context.UnitOfWork.TripRepository.Items.Single());

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
        var service = new RecurringTripService(
            unitOfWork,
            tripService,
            Options.Create(new RecurringTripSettings
            {
                GenerationHorizonDays = 14,
                GenerationIntervalMinutes = 15
            }));
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        return new RecurringTestContext(
            unitOfWork,
            driverId,
            vehicle,
            tripService,
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
                BookingWindowHours = 5,
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
            BookingWindowHours = request.BookingWindowHours,
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
        RecurringTripService Service,
        CreateRecurringTripRequestDto Request);
}
