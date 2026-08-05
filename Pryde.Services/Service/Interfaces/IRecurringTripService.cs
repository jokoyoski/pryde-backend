using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IRecurringTripService
{
    Task<RecurringTripResponseDto> CreateAsync(Guid driverId, CreateRecurringTripRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTripResponseDto>> GetMineAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> GetOwnedAsync(Guid recurringTripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> UpdateAsync(Guid recurringTripId, Guid driverId, UpdateRecurringTripRequestDto request, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> PauseAsync(Guid recurringTripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> ResumeAsync(Guid recurringTripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> CancelAsync(Guid recurringTripId, Guid driverId, CancellationToken cancellationToken = default);
    Task<TripSubscriptionResponseDto> SubscribeAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripSubscriptionResponseDto>> GetMySubscriptionsAsync(Guid passengerId, CancellationToken cancellationToken = default);
    Task<TripSubscriptionResponseDto> CancelSubscriptionAsync(Guid recurringTripId, Guid passengerId, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<RecurringTripResponseDto>> AdminGetAllAsync(AdminRecurringTripsRequestDto request, CancellationToken cancellationToken = default);
    Task<RecurringTripResponseDto> AdminGetByIdAsync(Guid recurringTripId, CancellationToken cancellationToken = default);
    Task<int> GenerateOccurrencesAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
