using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface ITripService
{
    Task<TripDetailsResponseDto> CreateAsync(Guid driverId, CreateTripRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripSummaryResponseDto>> SearchAsync(SearchTripsRequestDto request, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripSummaryResponseDto>> GetMineAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> UpdateAsync(Guid tripId, Guid driverId, UpdateTripRequestDto request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> StartAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> ConfirmPickupAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> EndAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> ConfirmDropoffAsync(Guid tripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripDetailsResponseDto> CompleteAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default);
}
