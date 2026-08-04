using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface ITripRatingService
{
    Task<TripRatingResponseDto> CreateAsync(
        Guid bookingId,
        Guid raterId,
        TripRatingRequestDto request,
        CancellationToken cancellationToken = default);
    Task<UserRatingSummaryResponseDto> GetSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<AdminUserRatingsResponseDto> AdminGetByUserIdAsync(
        Guid userId,
        AdminUserRatingsRequestDto request,
        CancellationToken cancellationToken = default);
}
