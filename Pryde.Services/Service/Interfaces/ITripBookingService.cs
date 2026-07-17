using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface ITripBookingService
{
    Task<TripBookingResponseDto> CreateAsync(Guid passengerId, Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBookingResponseDto>> GetMineAsync(Guid passengerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBookingResponseDto>> GetPendingForTripAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBookingResponseDto>> GetConfirmedPassengersAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripBookingResponseDto> ApproveAsync(Guid bookingId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripBookingResponseDto> DeclineAsync(Guid bookingId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripBookingResponseDto> CancelAsync(Guid bookingId, Guid passengerId, CancellationToken cancellationToken = default);
}
