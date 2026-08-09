using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public sealed class RecurringTripService(
    IUnitOfWork unitOfWork,
    ITripService tripService,
    IOptions<RecurringTripSettings> settings,
    IOptions<TripSettings> tripSettings) : IRecurringTripService
{
    private const RecurringDays AllDays = RecurringDays.Monday |
        RecurringDays.Tuesday | RecurringDays.Wednesday |
        RecurringDays.Thursday | RecurringDays.Friday |
        RecurringDays.Saturday | RecurringDays.Sunday;
    private readonly RecurringTripSettings _settings = settings.Value;
    private readonly TripSettings _tripSettings = tripSettings.Value;

    public async Task<RecurringTripResponseDto> CreateAsync(
        Guid driverId,
        CreateRecurringTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request);
        var bookingWindowMinutes = ResolveBookingWindowMinutes(
            request.BookingWindowMinutes);
        var firstOccurrence = GetFirstOccurrence(
            request.StartDate,
            request.EndDate,
            request.DaysOfWeek,
            request.DepartureTime,
            bookingWindowMinutes);
        await tripService.ValidateRecurringTemplateAsync(
            driverId,
            BuildTripRequest(
                request,
                firstOccurrence,
                bookingWindowMinutes),
            cancellationToken);

        var schedule = new RecurringTrip { DriverId = driverId };
        Apply(schedule, request, bookingWindowMinutes);
        await unitOfWork.RecurringTrips.CreateAsync(schedule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetOwnedAsync(schedule.Id, driverId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTripResponseDto>> GetMineAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await unitOfWork.RecurringTrips
            .GetByDriverIdAsync(driverId, cancellationToken);
        return schedules.Select(Map).ToList();
    }

    public async Task<RecurringTripResponseDto> GetOwnedAsync(
        Guid recurringTripId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetOwnedScheduleAsync(
            recurringTripId, driverId, false, cancellationToken);
        return Map(schedule);
    }

    public async Task<RecurringTripResponseDto> UpdateAsync(
        Guid recurringTripId,
        Guid driverId,
        UpdateRecurringTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request);
        var bookingWindowMinutes = ResolveBookingWindowMinutes(
            request.BookingWindowMinutes);
        var firstOccurrence = GetFirstOccurrence(
            request.StartDate,
            request.EndDate,
            request.DaysOfWeek,
            request.DepartureTime,
            bookingWindowMinutes);
        await tripService.ValidateRecurringTemplateAsync(
            driverId,
            BuildTripRequest(
                request,
                firstOccurrence,
                bookingWindowMinutes),
            cancellationToken);

        var schedule = await GetOwnedScheduleAsync(
            recurringTripId, driverId, true, cancellationToken);
        EnsureNotCancelled(schedule);
        var activeSubscriptions = schedule.Subscriptions.Count(s => s.IsActive);
        if (activeSubscriptions > request.AvailableSeats)
            throw new ConflictException(
                "Available seats cannot be lower than the active subscription count.");

        Apply(schedule, request, bookingWindowMinutes);
        unitOfWork.RecurringTrips.Update(schedule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetOwnedAsync(schedule.Id, driverId, cancellationToken);
    }

    public Task<RecurringTripResponseDto> PauseAsync(
        Guid recurringTripId,
        Guid driverId,
        CancellationToken cancellationToken = default) =>
        SetActivityAsync(recurringTripId, driverId, false, false, cancellationToken);

    public Task<RecurringTripResponseDto> ResumeAsync(
        Guid recurringTripId,
        Guid driverId,
        CancellationToken cancellationToken = default) =>
        SetActivityAsync(recurringTripId, driverId, true, false, cancellationToken);

    public Task<RecurringTripResponseDto> CancelAsync(
        Guid recurringTripId,
        Guid driverId,
        CancellationToken cancellationToken = default) =>
        SetActivityAsync(recurringTripId, driverId, false, true, cancellationToken);

    public async Task<TripSubscriptionResponseDto> SubscribeAsync(
        Guid recurringTripId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                if (!await unitOfWork.Users.ExistsByIdAsync(
                        passengerId, transactionToken))
                    throw new NotFoundException(nameof(User), passengerId);

                var schedule = await unitOfWork.RecurringTrips
                    .GetByIdForUpdateAsync(recurringTripId, transactionToken)
                    ?? throw new NotFoundException(
                        nameof(RecurringTrip), recurringTripId);
                if (!schedule.IsActive || schedule.CancelledAt.HasValue)
                    throw new ConflictException(
                        "Only an active recurring trip can be subscribed to.");
                if (schedule.EndDate.HasValue &&
                    schedule.EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
                    throw new ConflictException("This recurring trip has ended.");

                var existing = await unitOfWork.TripSubscriptions
                    .GetByRecurringTripAndPassengerAsync(
                        recurringTripId, passengerId, transactionToken);
                if (existing?.IsActive == true)
                    throw new ConflictException(
                        "The passenger is already subscribed to this recurring trip.");

                var activeCount = await unitOfWork.TripSubscriptions
                    .CountActiveAsync(recurringTripId, transactionToken);
                if (activeCount >= schedule.AvailableSeats)
                    throw new ConflictException(
                        "The recurring trip has reached its subscription capacity.");

                if (existing is null)
                {
                    existing = new TripSubscription
                    {
                        RecurringTripId = recurringTripId,
                        RecurringTrip = schedule,
                        PassengerId = passengerId,
                        IsActive = true
                    };
                    await unitOfWork.TripSubscriptions.CreateAsync(
                        existing, transactionToken);
                }
                else
                {
                    existing.IsActive = true;
                    existing.CancelledAt = null;
                    existing.RecurringTrip = schedule;
                    unitOfWork.TripSubscriptions.Update(existing);
                }

                await unitOfWork.SaveChangesAsync(transactionToken);
                return Map(existing);
            }, cancellationToken);
    }

    public async Task<IReadOnlyList<TripSubscriptionResponseDto>>
        GetMySubscriptionsAsync(
            Guid passengerId,
            CancellationToken cancellationToken = default)
    {
        var subscriptions = await unitOfWork.TripSubscriptions
            .GetByPassengerIdAsync(passengerId, cancellationToken);
        return subscriptions.Select(Map).ToList();
    }

    public async Task<TripSubscriptionResponseDto> CancelSubscriptionAsync(
        Guid recurringTripId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await unitOfWork.TripSubscriptions
            .GetByRecurringTripAndPassengerAsync(
                recurringTripId, passengerId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(TripSubscription), recurringTripId);
        if (subscription.IsActive)
        {
            subscription.IsActive = false;
            subscription.CancelledAt = DateTime.UtcNow;
            unitOfWork.TripSubscriptions.Update(subscription);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (subscription.RecurringTrip is null)
        {
            subscription.RecurringTrip = await unitOfWork.RecurringTrips
                .GetByIdAsync(recurringTripId, cancellationToken)
                ?? throw new NotFoundException(
                    nameof(RecurringTrip), recurringTripId);
        }
        return Map(subscription);
    }

    public async Task<PagedResponseDto<RecurringTripResponseDto>>
        AdminGetAllAsync(
            AdminRecurringTripsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.RecurringTrips.GetAllAsync(
            request.DriverId, request.IsActive, request.IsCancelled,
            request.PageNumber, request.PageSize, cancellationToken);
        return new PagedResponseDto<RecurringTripResponseDto>
        {
            Items = result.Items.Select(Map).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(
                result.TotalCount / (double)request.PageSize)
        };
    }

    public async Task<RecurringTripResponseDto> AdminGetByIdAsync(
        Guid recurringTripId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await unitOfWork.RecurringTrips.GetByIdAsync(
            recurringTripId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(RecurringTrip), recurringTripId);
        return Map(schedule);
    }

    public async Task<int> GenerateOccurrencesAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        utcNow = utcNow.ToUniversalTime();
        var from = DateOnly.FromDateTime(utcNow);
        var to = from.AddDays(_settings.GenerationHorizonDays);
        var schedules = await unitOfWork.RecurringTrips
            .GetActiveForGenerationAsync(from, to, cancellationToken);
        var generatedCount = 0;

        foreach (var schedule in schedules)
        {
            var firstDate = schedule.StartDate > from
                ? schedule.StartDate
                : from;
            var lastDate = schedule.EndDate.HasValue &&
                schedule.EndDate.Value < to
                    ? schedule.EndDate.Value
                    : to;
            for (var date = firstDate; date <= lastDate;
                 date = date.AddDays(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Includes(schedule.DaysOfWeek, date.DayOfWeek))
                    continue;

                var departure = ToUtc(date, schedule.DepartureTime);
                if (TripBookingWindow.GetClosesAtUtc(
                        departure,
                        schedule.BookingWindowMinutes) <= utcNow ||
                    await unitOfWork.Trips.RecurringOccurrenceExistsAsync(
                        schedule.Id, departure, cancellationToken))
                    continue;

                await tripService.CreateRecurringOccurrenceAsync(
                    schedule.DriverId,
                    schedule.Id,
                    BuildTripRequest(schedule, departure),
                    cancellationToken);
                generatedCount++;
            }
        }

        return generatedCount;
    }

    private async Task<RecurringTripResponseDto> SetActivityAsync(
        Guid recurringTripId,
        Guid driverId,
        bool isActive,
        bool cancel,
        CancellationToken cancellationToken)
    {
        var schedule = await GetOwnedScheduleAsync(
            recurringTripId, driverId, true, cancellationToken);
        if (schedule.CancelledAt.HasValue)
        {
            if (cancel)
                return Map(schedule);
            throw new ConflictException(
                "A cancelled recurring trip cannot be paused or resumed.");
        }

        if (cancel)
        {
            schedule.IsActive = false;
            schedule.CancelledAt = DateTime.UtcNow;
        }
        else
        {
            if (isActive && schedule.EndDate.HasValue &&
                schedule.EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ConflictException(
                    "An ended recurring trip cannot be resumed.");
            schedule.IsActive = isActive;
        }

        unitOfWork.RecurringTrips.Update(schedule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetOwnedAsync(schedule.Id, driverId, cancellationToken);
    }

    private async Task<RecurringTrip> GetOwnedScheduleAsync(
        Guid recurringTripId,
        Guid driverId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var schedule = forUpdate
            ? await unitOfWork.RecurringTrips.GetByIdForUpdateAsync(
                recurringTripId, cancellationToken)
            : await unitOfWork.RecurringTrips.GetByIdAsync(
                recurringTripId, cancellationToken);
        if (schedule is null)
            throw new NotFoundException(nameof(RecurringTrip), recurringTripId);
        if (schedule.DriverId != driverId)
            throw new ForbiddenException(
                "Only the recurring trip owner can perform this action.");
        return schedule;
    }

    private static void ValidateSchedule(CreateRecurringTripRequestDto request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
            throw new ValidationException("Start date cannot be in the past.");
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            throw new ValidationException("End date cannot be earlier than start date.");
        if (request.DaysOfWeek == RecurringDays.None ||
            (request.DaysOfWeek & ~AllDays) != 0)
            throw new ValidationException(
                "At least one valid recurring day is required.");
    }

    private static DateTime GetFirstOccurrence(
        DateOnly startDate,
        DateOnly? endDate,
        RecurringDays days,
        TimeOnly departureTime,
        int bookingWindowMinutes)
    {
        var utcNow = DateTime.UtcNow;
        for (var offset = 0; offset < 14; offset++)
        {
            var date = startDate.AddDays(offset);
            if (endDate.HasValue && date > endDate.Value)
                break;
            var departure = ToUtc(date, departureTime);
            if (Includes(days, date.DayOfWeek) &&
                TripBookingWindow.GetClosesAtUtc(
                    departure,
                    bookingWindowMinutes) > utcNow)
                return departure;
        }
        throw new ValidationException(
            "The selected date range contains no recurring day.");
    }

    private static bool Includes(RecurringDays days, DayOfWeek day) =>
        days.HasFlag(day switch
        {
            DayOfWeek.Monday => RecurringDays.Monday,
            DayOfWeek.Tuesday => RecurringDays.Tuesday,
            DayOfWeek.Wednesday => RecurringDays.Wednesday,
            DayOfWeek.Thursday => RecurringDays.Thursday,
            DayOfWeek.Friday => RecurringDays.Friday,
            DayOfWeek.Saturday => RecurringDays.Saturday,
            DayOfWeek.Sunday => RecurringDays.Sunday,
            _ => RecurringDays.None
        });

    private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);

    private int ResolveBookingWindowMinutes(int? bookingWindowMinutes) =>
        bookingWindowMinutes ?? _tripSettings.DefaultBookingWindowMinutes;

    private static CreateTripRequestDto BuildTripRequest(
        CreateRecurringTripRequestDto request,
        DateTime departureTime,
        int bookingWindowMinutes) => new()
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
            DepartureTime = departureTime,
            AvailableSeats = request.AvailableSeats,
            AllowLuggage = request.AllowLuggage,
            BookingWindowMinutes = bookingWindowMinutes
        };

    private static CreateTripRequestDto BuildTripRequest(
        RecurringTrip schedule,
        DateTime departureTime) => new()
        {
            VehicleId = schedule.VehicleId
                ?? throw new ConflictException(
                    "The recurring trip does not have a vehicle."),
            OriginLatitude = schedule.OriginLatitude,
            OriginLongitude = schedule.OriginLongitude,
            OriginAddress = schedule.OriginAddress,
            DestinationLatitude = schedule.DestinationLatitude,
            DestinationLongitude = schedule.DestinationLongitude,
            DestinationAddress = schedule.DestinationAddress,
            DistanceKm = schedule.DistanceKm,
            EstimatedDurationMinutes = schedule.EstimatedDurationMinutes,
            RoutePolyline = schedule.RoutePolyline,
            DepartureTime = departureTime,
            AvailableSeats = schedule.AvailableSeats,
            AllowLuggage = schedule.AllowLuggage,
            BookingWindowMinutes = schedule.BookingWindowMinutes
        };

    private static void Apply(
        RecurringTrip schedule,
        CreateRecurringTripRequestDto request,
        int bookingWindowMinutes)
    {
        schedule.VehicleId = request.VehicleId;
        schedule.OriginLatitude = request.OriginLatitude;
        schedule.OriginLongitude = request.OriginLongitude;
        schedule.OriginAddress = request.OriginAddress.Trim();
        schedule.DestinationLatitude = request.DestinationLatitude;
        schedule.DestinationLongitude = request.DestinationLongitude;
        schedule.DestinationAddress = request.DestinationAddress.Trim();
        schedule.DistanceKm = request.DistanceKm;
        schedule.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        schedule.RoutePolyline = string.IsNullOrWhiteSpace(request.RoutePolyline)
            ? null
            : request.RoutePolyline.Trim();
        schedule.AvailableSeats = request.AvailableSeats;
        schedule.AllowLuggage = request.AllowLuggage;
        schedule.BookingWindowMinutes = bookingWindowMinutes;
        schedule.StartDate = request.StartDate;
        schedule.EndDate = request.EndDate;
        schedule.DaysOfWeek = request.DaysOfWeek;
        schedule.DepartureTime = request.DepartureTime;
    }

    private static void EnsureNotCancelled(RecurringTrip schedule)
    {
        if (schedule.CancelledAt.HasValue)
            throw new ConflictException(
                "A cancelled recurring trip cannot be updated.");
    }

    private static RecurringTripResponseDto Map(RecurringTrip schedule) => new()
    {
        RecurringTripId = schedule.Id,
        DriverId = schedule.DriverId,
        DriverName = schedule.Driver?.Profile is null
            ? string.Empty
            : $"{schedule.Driver.Profile.FirstName} {schedule.Driver.Profile.LastName}".Trim(),
        VehicleId = schedule.VehicleId,
        VehicleLicensePlateNumber = schedule.Vehicle?.LicensePlateNumber ?? string.Empty,
        OriginLatitude = schedule.OriginLatitude,
        OriginLongitude = schedule.OriginLongitude,
        OriginAddress = schedule.OriginAddress,
        DestinationLatitude = schedule.DestinationLatitude,
        DestinationLongitude = schedule.DestinationLongitude,
        DestinationAddress = schedule.DestinationAddress,
        DistanceKm = schedule.DistanceKm,
        EstimatedDurationMinutes = schedule.EstimatedDurationMinutes,
        RoutePolyline = schedule.RoutePolyline,
        AvailableSeats = schedule.AvailableSeats,
        AllowLuggage = schedule.AllowLuggage,
        BookingWindowMinutes = schedule.BookingWindowMinutes,
        StartDate = schedule.StartDate,
        EndDate = schedule.EndDate,
        DaysOfWeek = schedule.DaysOfWeek,
        DepartureTime = schedule.DepartureTime,
        IsActive = schedule.IsActive,
        IsCancelled = schedule.CancelledAt.HasValue,
        CancelledAt = schedule.CancelledAt,
        ActiveSubscriptionCount = schedule.Subscriptions.Count(s => s.IsActive),
        GeneratedTrips = schedule.Trips
            .OrderBy(t => t.DepartureTime)
            .Select(t => new RecurringTripOccurrenceResponseDto
            {
                TripId = t.Id,
                DepartureTime = t.DepartureTime,
                Status = t.Status,
                AvailableSeats = t.AvailableSeats
            }).ToList(),
        CreatedAt = schedule.CreatedAt
    };

    private static TripSubscriptionResponseDto Map(
        TripSubscription subscription) => new()
        {
            SubscriptionId = subscription.Id,
            RecurringTripId = subscription.RecurringTripId,
            PassengerId = subscription.PassengerId,
            IsActive = subscription.IsActive,
            CancelledAt = subscription.CancelledAt,
            OriginAddress = subscription.RecurringTrip.OriginAddress,
            DestinationAddress = subscription.RecurringTrip.DestinationAddress,
            DaysOfWeek = subscription.RecurringTrip.DaysOfWeek,
            DepartureTime = subscription.RecurringTrip.DepartureTime,
            CreatedAt = subscription.CreatedAt
        };
}
