using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface ITripRatingRepository
{
    Task<bool> ExistsAsync(
        Guid bookingId,
        Guid raterId,
        CancellationToken cancellationToken = default);
    Task<RatingSummaryData> GetSummaryAsync(
        Guid ratedUserId,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AdminTripRatingData> Items, int TotalCount)>
        GetAdminByRatedUserIdAsync(
            Guid ratedUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    Task<TripRating> CreateAsync(
        TripRating rating,
        CancellationToken cancellationToken = default);
}

public sealed record RatingSummaryData(
    double AverageRating,
    int RatingCount);

public sealed record AdminTripRatingData(
    Guid RatingId,
    Guid BookingId,
    Guid TripId,
    int Value,
    string? Comment,
    Guid RaterUserId,
    string RaterName,
    string RaterRole,
    Guid RatedUserId,
    string TripOrigin,
    string TripDestination,
    DateTime CreatedAt);
