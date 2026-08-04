using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Constants;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class TripRatingRepository(PrydeDbContext context)
    : ITripRatingRepository
{
    public Task<bool> ExistsAsync(
        Guid bookingId,
        Guid raterId,
        CancellationToken cancellationToken = default)
    {
        return context.TripRatings.AnyAsync(
            rating => rating.BookingId == bookingId &&
                      rating.RaterId == raterId,
            cancellationToken);
    }

    public async Task<RatingSummaryData> GetSummaryAsync(
        Guid ratedUserId,
        CancellationToken cancellationToken = default)
    {
        var summary = await context.TripRatings
            .AsNoTracking()
            .Where(rating => rating.RatedUserId == ratedUserId)
            .GroupBy(_ => 1)
            .Select(group => new RatingSummaryData(
                group.Average(rating => rating.Value),
                group.Count()))
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new RatingSummaryData(0, 0);
    }

    public async Task<(
        IReadOnlyList<AdminTripRatingData> Items,
        int TotalCount)> GetAdminByRatedUserIdAsync(
            Guid ratedUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var query = context.TripRatings
            .AsNoTracking()
            .Where(rating =>
                rating.RatedUserId == ratedUserId);
        var totalCount = await query.CountAsync(
            cancellationToken);
        var items = await query
            .OrderByDescending(rating => rating.CreatedAt)
            .ThenByDescending(rating => rating.Id)
            .Select(rating => new AdminTripRatingData(
                rating.Id,
                rating.BookingId,
                rating.Booking.TripId,
                rating.Value,
                rating.Comment,
                rating.RaterId,
                rating.Rater.Profile == null
                    ? string.Empty
                    : (rating.Rater.Profile.FirstName + " " +
                       rating.Rater.Profile.LastName).Trim(),
                rating.RaterId ==
                    rating.Booking.Trip.DriverId
                        ? RoleNames.Driver
                        : RoleNames.Passenger,
                rating.RatedUserId,
                rating.Booking.Trip.OriginAddress,
                rating.Booking.Trip.DestinationAddress,
                rating.CreatedAt))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<TripRating> CreateAsync(
        TripRating rating,
        CancellationToken cancellationToken = default)
    {
        await context.TripRatings.AddAsync(
            rating,
            cancellationToken);
        return rating;
    }
}
