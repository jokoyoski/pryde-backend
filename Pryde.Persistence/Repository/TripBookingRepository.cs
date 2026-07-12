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

    public async Task<IReadOnlyList<TripBooking>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings.AsNoTracking().Where(b => b.TripId == tripId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TripBooking>> GetByPassengerIdAsync(Guid passengerId, CancellationToken cancellationToken = default)
    {
        return await context.TripBookings.AsNoTracking().Where(b => b.PassengerId == passengerId).ToListAsync(cancellationToken);
    }

    public async Task<TripBooking> CreateAsync(TripBooking booking, CancellationToken cancellationToken = default)
    {
        await context.TripBookings.AddAsync(booking, cancellationToken);
        return booking;
    }

    public void Update(TripBooking booking) => context.TripBookings.Update(booking);
}