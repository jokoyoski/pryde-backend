using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class TripRepository(PrydeDbContext context) : ITripRepository
{
    public async Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .Include(t => t.Vehicle)
            .Include(t => t.Bookings)
                .ThenInclude(booking => booking.Escrow)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithVehicleForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var trip = await context.Trips
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "Trips"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (trip is not null)
        {
            await context.Entry(trip)
                .Reference(item => item.Vehicle)
                .LoadAsync(cancellationToken);
        }

        return trip;
    }

    public async Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow,
        DateTime? departureDate,
        bool? requiresLuggage,
        int requiredSeats,
        CancellationToken cancellationToken = default)
    {
        var query = context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Where(t => t.Status == TripStatus.Scheduled
                && t.DepartureTime > utcNow
                && t.DepartureTime.AddHours(-t.BookingWindowHours) > utcNow
                && t.AvailableSeats >= requiredSeats);

        if (departureDate.HasValue)
        {
            var start = DateTime.SpecifyKind(departureDate.Value.Date, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(t => t.DepartureTime >= start && t.DepartureTime < end);
        }

        if (requiresLuggage == true)
            query = query.Where(t => t.AllowLuggage);

        return await query
            .OrderBy(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(t => t.Status == TripStatus.Scheduled && t.AvailableSeats > 0)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>>
        GetExpiredConfirmationTripIdsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.Status ==
                    TripStatus.DropoffConfirmationPending &&
                trip.DriverEndedAt.HasValue &&
                trip.ConfirmationDeadline.HasValue &&
                trip.ConfirmationDeadline.Value <= utcNow)
            .OrderBy(trip => trip.ConfirmationDeadline)
            .Select(trip => trip.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Trip> CreateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await context.Trips.AddAsync(trip, cancellationToken);
        return trip;
    }

    public void Update(Trip trip) => context.Trips.Update(trip);
    public void Delete(Trip trip) => context.Trips.Remove(trip);
}
