using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
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

public class TripBookingService(
    IUnitOfWork unitOfWork,
    IFinancialService financialService,
    IOptions<BookingPaymentSettings> bookingPaymentOptions,
    INotificationService notificationService)
    : ITripBookingService
{
    private readonly BookingPaymentSettings _bookingPaymentSettings =
        bookingPaymentOptions.Value;

    public TripBookingService(IUnitOfWork unitOfWork)
        : this(
            unitOfWork,
            new FinancialService(unitOfWork),
            Options.Create(new BookingPaymentSettings()),
            new NotificationService(unitOfWork))
    {
    }

    public TripBookingService(
        IUnitOfWork unitOfWork,
        IFinancialService financialService)
        : this(
            unitOfWork,
            financialService,
            Options.Create(new BookingPaymentSettings()),
            new NotificationService(unitOfWork))
    {
    }

    public TripBookingService(
        IUnitOfWork unitOfWork,
        IFinancialService financialService,
        IOptions<BookingPaymentSettings> bookingPaymentOptions)
        : this(
            unitOfWork,
            financialService,
            bookingPaymentOptions,
            new NotificationService(unitOfWork))
    {
    }

    public async Task<TripBookingResponseDto> CreateAsync(
        Guid passengerId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
            throw new ValidationException("Trip ID is required.");

        var trip = await unitOfWork.Trips.GetByIdForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        EnsureTripOpenForBooking(trip);
        if (trip.DriverId == passengerId)
            throw new ConflictException("A driver cannot book their own trip.");
        if (await unitOfWork.TripBookings.HasActiveBookingAsync(tripId, passengerId, cancellationToken))
            throw new ConflictException("You already have an active booking for this trip.");

        var serviceCharge = Math.Round(trip.SeatPrice * trip.ServiceChargePercentage / 100m, 2);
        var booking = new TripBooking
        {
            TripId = trip.Id,
            PassengerId = passengerId,
            Status = BookingStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            SeatPrice = trip.SeatPrice,
            ServiceCharge = serviceCharge,
            TotalAmount = trip.SeatPrice + serviceCharge
        };

        await unitOfWork.TripBookings.CreateAsync(booking, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw new ConflictException("You already have an active booking for this trip.");
        }

        var profile = await unitOfWork.Profiles.GetByUserIdAsync(passengerId, cancellationToken);
        var response = WorkflowResponse(
            booking,
            trip,
            profile is null ? null : GetName(profile),
            WorkflowNextAction.AwaitDriverDecision,
            WorkflowActor.Driver);
        await notificationService.TryCreateAsync(
            NewNotification(
                trip.DriverId,
                NotificationType.BookingRequested,
                "New booking request",
                "A passenger requested a seat on your trip.",
                booking.Id,
                $"booking-request-received:{booking.Id}"),
            cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<TripBookingResponseDto>> GetMineAsync(
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var bookings = await unitOfWork.TripBookings.GetByPassengerIdAsync(passengerId, cancellationToken);
        var profile = await unitOfWork.Profiles.GetByUserIdAsync(passengerId, cancellationToken);
        var passengerName = profile is null ? null : GetName(profile);
        return bookings.Select(b => MapResponse(b, b.Trip, passengerName)).ToList();
    }

    public async Task<IReadOnlyList<TripBookingResponseDto>> GetPendingForTripAsync(
        Guid tripId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTripOwnerAsync(tripId, driverId, cancellationToken);
        var bookings = await unitOfWork.TripBookings.GetPendingByTripIdAsync(tripId, cancellationToken);
        return bookings.Select(b => MapResponse(b, b.Trip, GetPassengerName(b))).ToList();
    }

    public async Task<PagedResponseDto<DriverPendingBookingRequestResponseDto>>
        GetPendingForDriverAsync(
            Guid driverId,
            DriverBookingRequestsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.TripBookings
            .GetPendingByDriverIdAsync(
                driverId,
                request.PageNumber,
                request.PageSize,
                cancellationToken);

        var items = result.Items.Select(booking =>
            new DriverPendingBookingRequestResponseDto
            {
                BookingId = booking.BookingId,
                TripId = booking.TripId,
                PassengerId = booking.PassengerId,
                PassengerName = booking.PassengerName,
                PassengerProfileImageUrl =
                    booking.PassengerProfileImageUrl,
                RequestedSeats = 1,
                PickupLocation = booking.PickupLocation,
                Destination = booking.Destination,
                TripDepartureTime = booking.TripDepartureTime,
                RequestedAt = booking.RequestedAt
            }).ToList();

        return new PagedResponseDto<DriverPendingBookingRequestResponseDto>
        {
            Items = items,
            TotalCount = result.TotalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(
                result.TotalCount / (double)request.PageSize)
        };
    }

    public async Task<IReadOnlyList<TripBookingResponseDto>> GetConfirmedPassengersAsync(
        Guid tripId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTripOwnerAsync(tripId, driverId, cancellationToken);
        var bookings = await unitOfWork.TripBookings.GetApprovedByTripIdAsync(tripId, cancellationToken);
        return bookings.Select(b => MapResponse(b, b.Trip, GetPassengerName(b))).ToList();
    }

    public async Task<TripBookingResponseDto> ApproveAsync(
        Guid bookingId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var booking = await GetOwnedPendingBookingAsync(bookingId, driverId, cancellationToken);
        EnsureTripOpenForBooking(booking.Trip);

        booking.Status = BookingStatus.Approved;
        var approvedAt = DateTime.UtcNow;
        var paymentWindowEndsAt = approvedAt.AddMinutes(
            _bookingPaymentSettings.PaymentWindowMinutes);
        booking.ApprovedAt = approvedAt;
        booking.PaymentExpiresAt = paymentWindowEndsAt <
            booking.Trip.DepartureTime
                ? paymentWindowEndsAt
                : booking.Trip.DepartureTime;
        booking.Trip.AvailableSeats--;
        if (booking.Trip.AvailableSeats < 0)
        {
            throw new ConflictException("No seats are available for this trip.");
        }

        unitOfWork.TripBookings.Update(booking);
        unitOfWork.Trips.Update(booking.Trip);
        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        var response = WorkflowResponse(
            booking,
            booking.Trip,
            GetPassengerName(booking),
            WorkflowNextAction.PayForBooking,
            WorkflowActor.Passenger);
        await notificationService.TryCreateAsync(
            NewNotification(
                booking.PassengerId,
                NotificationType.BookingApproved,
                "Booking approved",
                "Your booking request was approved. Complete payment to confirm your seat.",
                booking.Id,
                $"booking-approved:{booking.Id}"),
            cancellationToken);
        return response;
    }

    public async Task<TripBookingResponseDto> DeclineAsync(
        Guid bookingId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var booking = await GetOwnedPendingBookingAsync(bookingId, driverId, cancellationToken);
        booking.Status = BookingStatus.Declined;
        unitOfWork.TripBookings.Update(booking);
        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        var response = WorkflowResponse(
            booking,
            booking.Trip,
            GetPassengerName(booking),
            WorkflowNextAction.None,
            WorkflowActor.None);
        await notificationService.TryCreateAsync(
            NewNotification(
                booking.PassengerId,
                NotificationType.BookingDeclined,
                "Booking declined",
                "Your booking request was declined.",
                booking.Id,
                $"booking-declined:{booking.Id}"),
            cancellationToken);
        return response;
    }

    public async Task<EscrowResponseDto> PayAsync(
        Guid bookingId, Guid passengerId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var response = await financialService.HoldBookingPaymentAsync(
            passengerId,
            bookingId,
            idempotencyKey,
            cancellationToken);
        response.NextAction = WorkflowNextAction.DriverStartTrip;
        response.RequiredActor = WorkflowActor.Driver;
        return response;
    }

    public async Task<TripBookingResponseDto> CancelAsync(
        Guid bookingId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var booking = await unitOfWork.TripBookings.GetByIdWithTripAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(TripBooking), bookingId);
        if (booking.PassengerId != passengerId)
            throw new ForbiddenException("Only the booking owner can cancel this booking.");
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Approved))
        {
            throw new ConflictException("This booking can no longer be cancelled.");
        }

        if (booking.Trip.Status is not (
                TripStatus.Scheduled or
                TripStatus.BookingClosed))
        {
            throw new ConflictException(
                "The booking cannot be cancelled after the trip has started.");
        }

        if (booking.Status == BookingStatus.Approved)
        {
            BookingSeatReservation.CancelApprovedBooking(booking);
            unitOfWork.Trips.Update(booking.Trip);
        }
        else
        {
            booking.Status = BookingStatus.Cancelled;
        }
        unitOfWork.TripBookings.Update(booking);
        if (booking.PaidAt.HasValue)
        {
            await financialService.RefundBookingAsync(booking.Id, cancellationToken);
        }
        else
        {
            await SaveWithConcurrencyHandlingAsync(cancellationToken);
        }

        var response = MapResponse(
            booking,
            booking.Trip,
            GetPassengerName(booking));
        await notificationService.TryCreateAsync(
            NewNotification(
                booking.Trip.DriverId,
                NotificationType.BookingCancelled,
                "Booking cancelled",
                "A passenger cancelled a booking on your trip.",
                booking.Id,
                $"booking-cancelled:{booking.Id}:{booking.Trip.DriverId}"),
            cancellationToken);
        return response;
    }

    private static CreateNotificationRequest NewNotification(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid bookingId,
        string deduplicationKey)
    {
        return new CreateNotificationRequest
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = bookingId,
            RelatedEntityType = nameof(TripBooking),
            DeduplicationKey = deduplicationKey
        };
    }

    private async Task EnsureTripOwnerAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken)
    {
        var trip = await unitOfWork.Trips.GetByIdAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        if (trip.DriverId != driverId)
            throw new ForbiddenException("Only the trip owner can access this information.");
    }

    private async Task<TripBooking> GetOwnedPendingBookingAsync(
        Guid bookingId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var booking = await unitOfWork.TripBookings.GetByIdWithTripAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(TripBooking), bookingId);
        if (booking.Trip.DriverId != driverId)
            throw new ForbiddenException("Only the trip owner can decide this booking request.");
        if (booking.Status != BookingStatus.Pending)
            throw new ConflictException("Only a pending booking request can be approved or declined.");
        return booking;
    }

    private static void EnsureTripOpenForBooking(Trip trip)
    {
        var now = DateTime.UtcNow;
        if (trip.Status != TripStatus.Scheduled)
            throw new ConflictException("This trip is not open for booking requests.");
        if (trip.DepartureTime <= now)
            throw new ConflictException("This trip has already departed.");
        if (TripBookingWindow.GetClosesAtUtc(
                trip.DepartureTime,
                trip.BookingWindowMinutes) <= now)
            throw new ConflictException("The booking request window for this trip is closed.");
        if (trip.AvailableSeats <= 0)
            throw new ConflictException("No seats are available for this trip.");
    }

    private async Task SaveWithConcurrencyHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The booking changed while this request was being processed. Please try again.");
        }
    }

    private static TripBookingResponseDto MapResponse(TripBooking booking, Trip trip, string? passengerName)
    {
        return new TripBookingResponseDto
        {
            BookingId = booking.Id,
            TripId = booking.TripId,
            PassengerId = booking.PassengerId,
            PassengerName = passengerName,
            TripOrigin = trip.OriginAddress,
            TripDestination = trip.DestinationAddress,
            DepartureTime = trip.DepartureTime,
            SeatPrice = booking.SeatPrice,
            ServiceCharge = booking.ServiceCharge,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            RequestedAt = booking.RequestedAt,
            ApprovedAt = booking.ApprovedAt,
            PaidAt = booking.PaidAt,
            IsPaid = booking.PaidAt.HasValue,
            PaymentExpiresAt = booking.PaymentExpiresAt
        };
    }

    private static TripBookingResponseDto WorkflowResponse(
        TripBooking booking,
        Trip trip,
        string? passengerName,
        WorkflowNextAction nextAction,
        WorkflowActor requiredActor)
    {
        var response = MapResponse(
            booking,
            trip,
            passengerName);
        response.NextAction = nextAction;
        response.RequiredActor = requiredActor;
        return response;
    }

    private static string? GetPassengerName(TripBooking booking) =>
        booking.Passenger?.Profile is null ? null : GetName(booking.Passenger.Profile);

    private static string GetName(Profile profile) => $"{profile.FirstName} {profile.LastName}".Trim();
}
