using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class TripBookingService(
    IUnitOfWork unitOfWork,
    IFinancialService financialService) : ITripBookingService
{
    public TripBookingService(IUnitOfWork unitOfWork)
        : this(unitOfWork, new FinancialService(unitOfWork))
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
        return MapResponse(booking, trip, profile is null ? null : GetName(profile));
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
        booking.ApprovedAt = DateTime.UtcNow;
        booking.Trip.AvailableSeats--;
        if (booking.Trip.AvailableSeats < 0)
            throw new ConflictException("No seats are available for this trip.");

        unitOfWork.TripBookings.Update(booking);
        unitOfWork.Trips.Update(booking.Trip);
        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        return MapResponse(booking, booking.Trip, GetPassengerName(booking));
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
        return MapResponse(booking, booking.Trip, GetPassengerName(booking));
    }

    public Task<EscrowResponseDto> PayAsync(
        Guid bookingId, Guid passengerId, string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        financialService.HoldBookingPaymentAsync(
            passengerId, bookingId, idempotencyKey, cancellationToken);

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
            throw new ConflictException("This booking can no longer be cancelled.");
        if (booking.Status == BookingStatus.Approved
            && (booking.Trip.DepartureTime <= DateTime.UtcNow
                || booking.Trip.Status is TripStatus.InProgress or TripStatus.Completed))
            throw new ConflictException("An approved booking cannot be cancelled after the trip has started.");

        if (booking.Status == BookingStatus.Approved)
        {
            booking.Trip.AvailableSeats = Math.Min(
                booking.Trip.Vehicle.Capacity,
                booking.Trip.AvailableSeats + 1);
            unitOfWork.Trips.Update(booking.Trip);
        }

        booking.Status = BookingStatus.Cancelled;
        unitOfWork.TripBookings.Update(booking);
        if (booking.PaidAt.HasValue)
            await financialService.RefundBookingAsync(booking.Id, cancellationToken);
        else
            await SaveWithConcurrencyHandlingAsync(cancellationToken);
        return MapResponse(booking, booking.Trip, GetPassengerName(booking));
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
        if (trip.DepartureTime - TimeSpan.FromHours(trip.BookingWindowHours) <= now)
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
            ApprovedAt = booking.ApprovedAt
        };
    }

    private static string? GetPassengerName(TripBooking booking) =>
        booking.Passenger?.Profile is null ? null : GetName(booking.Passenger.Profile);

    private static string GetName(Profile profile) => $"{profile.FirstName} {profile.LastName}".Trim();
}
