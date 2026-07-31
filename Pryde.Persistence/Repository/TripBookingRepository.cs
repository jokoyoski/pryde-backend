using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class TripBookingRepository(PrydeDbContext context) : ITripBookingRepository
{
    public async Task<TripBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<TripBooking?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "TripBookings"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TripBooking?> GetByIdWithTripAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .Include(b => b.Trip)
                .ThenInclude(t => t.Vehicle)
            .Include(b => b.Passenger)
                .ThenInclude(p => p.Profile)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .Include(b => b.Passenger)
                .ThenInclude(p => p.Profile)
            .Include(b => b.Trip)
            .Where(b => b.TripId == tripId)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TripBooking>> GetPendingByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .Include(b => b.Passenger)
                .ThenInclude(p => p.Profile)
            .Include(b => b.Trip)
            .Where(b => b.TripId == tripId && b.Status == Pryde.Domain.Enums.BookingStatus.Pending)
            .OrderBy(b => b.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TripBooking>> GetApprovedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .Include(b => b.Passenger)
                .ThenInclude(p => p.Profile)
            .Include(b => b.Trip)
            .Where(b => b.TripId == tripId && b.Status == Pryde.Domain.Enums.BookingStatus.Approved)
            .OrderBy(b => b.ApprovedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .Include(b => b.Trip)
            .Where(b => b.PassengerId == passengerId)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountPendingByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .CountAsync(
                booking =>
                    booking.Trip.DriverId == driverId &&
                    booking.Status == Pryde.Domain.Enums.BookingStatus.Pending,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>>
        GetExpiredUnpaidApprovedBookingIdsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        return await context.TripBookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status ==
                    Pryde.Domain.Enums.BookingStatus.Approved &&
                booking.PaidAt == null &&
                booking.PaymentExpiresAt.HasValue &&
                booking.PaymentExpiresAt.Value <= utcNow)
            .OrderBy(booking => booking.PaymentExpiresAt)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveBookingAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings.AnyAsync(
            b => b.TripId == tripId
                && b.PassengerId == passengerId
                && (b.Status == Pryde.Domain.Enums.BookingStatus.Pending
                    || b.Status == Pryde.Domain.Enums.BookingStatus.Approved),
            cancellationToken);
    }

    public async Task<TripBooking> CreateAsync(TripBooking booking, CancellationToken cancellationToken = default)
    {
        await context.TripBookings.AddAsync(booking, cancellationToken);
        return booking;
    }

    public void Update(TripBooking booking) => context.TripBookings.Update(booking);
}
