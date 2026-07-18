using Pryde.Domain.Entities;
using Pryde.Domain.Enums;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IEscrowRepository
{
    Task<Escrow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Escrow?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Escrow>> GetHeldByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Escrow> Items, int TotalCount)> GetAsync(EscrowStatus? status, Guid? bookingId, Guid? passengerId, Guid? driverId, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<EscrowTotals> GetTotalsAsync(CancellationToken cancellationToken = default);
    Task<Escrow> CreateAsync(Escrow escrow, CancellationToken cancellationToken = default);
    void Update(Escrow escrow);
}

public sealed record EscrowTotals(decimal Held, decimal Released, decimal Refunded);
