using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class EscrowRepository(PrydeDbContext context) : IEscrowRepository
{
    public Task<Escrow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Details().AsNoTracking().FirstOrDefaultAsync(escrow => escrow.Id == id, cancellationToken);

    public Task<Escrow?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        Details().FirstOrDefaultAsync(escrow => escrow.BookingId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<Escrow>> GetHeldByTripIdAsync(
        Guid tripId, CancellationToken cancellationToken = default) =>
        await Details()
            .Where(escrow => escrow.Booking.TripId == tripId && escrow.Status == EscrowStatus.Held)
            .OrderBy(escrow => escrow.HeldAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Escrow> Items, int TotalCount)> GetAsync(
        EscrowStatus? status, Guid? bookingId, Guid? passengerId, Guid? driverId,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Escrows.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(escrow => escrow.Status == status.Value);
        if (bookingId.HasValue) query = query.Where(escrow => escrow.BookingId == bookingId.Value);
        if (passengerId.HasValue) query = query.Where(escrow => escrow.PassengerId == passengerId.Value);
        if (driverId.HasValue) query = query.Where(escrow => escrow.DriverId == driverId.Value);
        if (dateFrom.HasValue) query = query.Where(escrow => escrow.HeldAt >= dateFrom.Value.ToUniversalTime());
        if (dateTo.HasValue) query = query.Where(escrow => escrow.HeldAt <= dateTo.Value.ToUniversalTime());
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(escrow => escrow.Booking).ThenInclude(booking => booking.Passenger).ThenInclude(passenger => passenger.Profile)
            .Include(escrow => escrow.Booking).ThenInclude(booking => booking.Trip).ThenInclude(trip => trip.Driver).ThenInclude(driver => driver.Profile)
            .OrderByDescending(escrow => escrow.HeldAt).ThenBy(escrow => escrow.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<EscrowTotals> GetTotalsAsync(CancellationToken cancellationToken = default)
    {
        var held = await context.Escrows.Where(escrow => escrow.Status == EscrowStatus.Held)
            .SumAsync(escrow => (decimal?)escrow.Amount, cancellationToken) ?? 0;
        var released = await context.Escrows.Where(escrow => escrow.Status == EscrowStatus.Released)
            .SumAsync(escrow => (decimal?)escrow.Amount, cancellationToken) ?? 0;
        var refunded = await context.Escrows.Where(escrow => escrow.Status == EscrowStatus.Refunded)
            .SumAsync(escrow => (decimal?)escrow.Amount, cancellationToken) ?? 0;
        return new EscrowTotals(held, released, refunded);
    }

    public async Task<Escrow> CreateAsync(Escrow escrow, CancellationToken cancellationToken = default)
    {
        await context.Escrows.AddAsync(escrow, cancellationToken);
        return escrow;
    }

    public void Update(Escrow escrow) => context.Escrows.Update(escrow);

    private IQueryable<Escrow> Details() => context.Escrows
        .Include(escrow => escrow.Booking).ThenInclude(booking => booking.Passenger).ThenInclude(passenger => passenger.Profile)
        .Include(escrow => escrow.Booking).ThenInclude(booking => booking.Trip).ThenInclude(trip => trip.Driver).ThenInclude(driver => driver.Profile);
}
