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

    public async Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(t => t.Status == TripStatus.Scheduled && t.AvailableSeats > 0)
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