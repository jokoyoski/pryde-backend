using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class TripRatingService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    TimeProvider? timeProvider = null) : ITripRatingService
{
    private const int MaximumCommentLength = 1000;
    private static readonly TimeSpan RatingWindow =
        TimeSpan.FromHours(24);
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public async Task<TripRatingResponseDto> CreateAsync(
        Guid bookingId,
        Guid raterId,
        TripRatingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var booking = await unitOfWork.TripBookings
            .GetByIdWithTripAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(TripBooking), bookingId);

        if (booking.Status != BookingStatus.Completed ||
            booking.Trip.Status != TripStatus.Completed)
        {
            throw new ConflictException(
                "Ratings can only be submitted for completed bookings and trips.");
        }

        var ratedUserId = GetRatedUserId(booking, raterId);
        var completedAt = booking.Trip.CompletedAt ??
            booking.Trip.AutoCompletedAt ??
            booking.Trip.UpdatedAt;
        if (!completedAt.HasValue)
        {
            throw new ConflictException(
                "The trip completion time is unavailable.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now > completedAt.Value.Add(RatingWindow))
        {
            throw new ConflictException(
                "The 24-hour rating window has expired.");
        }

        if (await unitOfWork.TripRatings.ExistsAsync(
                bookingId,
                raterId,
                cancellationToken))
        {
            throw new ConflictException(
                "You have already rated this booking.");
        }

        var rating = new TripRating
        {
            BookingId = bookingId,
            RaterId = raterId,
            RatedUserId = ratedUserId,
            Value = request.Value,
            Comment = NormalizeComment(request.Comment),
            CreatedAt = now
        };

        await unitOfWork.TripRatings.CreateAsync(
            rating,
            cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateRating(exception))
        {
            throw new ConflictException(
                "You have already rated this booking.");
        }

        await notificationService.TryCreateAsync(
            new CreateNotificationRequest
            {
                UserId = ratedUserId,
                Type = NotificationType.RatingReceived,
                Title = "New rating received",
                Message = "You received a rating for a completed trip.",
                RelatedEntityId = bookingId,
                RelatedEntityType = nameof(TripBooking),
                DeduplicationKey = $"rating-received:{rating.Id}"
            },
            cancellationToken);

        return Map(rating);
    }

    public async Task<UserRatingSummaryResponseDto> GetSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Users.ExistsByIdAsync(
                userId,
                cancellationToken))
        {
            throw new NotFoundException(nameof(User), userId);
        }

        var summary = await unitOfWork.TripRatings
            .GetSummaryAsync(userId, cancellationToken);
        return new UserRatingSummaryResponseDto
        {
            UserId = userId,
            AverageRating = Math.Round(summary.AverageRating, 2),
            RatingCount = summary.RatingCount
        };
    }

    public async Task<AdminUserRatingsResponseDto>
        AdminGetByUserIdAsync(
            Guid userId,
            AdminUserRatingsRequestDto request,
            CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Users.ExistsByIdAsync(
                userId,
                cancellationToken))
        {
            throw new NotFoundException(nameof(User), userId);
        }

        request ??= new AdminUserRatingsRequestDto();
        var ratings = await unitOfWork.TripRatings
            .GetAdminByRatedUserIdAsync(
                userId,
                request.PageNumber,
                request.PageSize,
                cancellationToken);
        var summary = await unitOfWork.TripRatings
            .GetSummaryAsync(userId, cancellationToken);
        var averageRating = Math.Round(
            summary.AverageRating,
            2);

        return new AdminUserRatingsResponseDto
        {
            UserId = userId,
            AverageRating = averageRating,
            TotalRatings = summary.RatingCount,
            RatingPercentage = Math.Round(
                averageRating / 5d * 100d,
                2),
            Items = ratings.Items.Select(MapAdmin).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = ratings.TotalCount,
            TotalPages = (int)Math.Ceiling(
                ratings.TotalCount / (double)request.PageSize)
        };
    }

    private static Guid GetRatedUserId(
        TripBooking booking,
        Guid raterId)
    {
        if (raterId == booking.PassengerId)
        {
            return booking.Trip.DriverId;
        }

        if (raterId == booking.Trip.DriverId)
        {
            return booking.PassengerId;
        }

        throw new ForbiddenException(
            "Only the driver or passenger for this booking can submit a rating.");
    }

    private static void ValidateRequest(TripRatingRequestDto request)
    {
        if (request is null)
        {
            throw new ValidationException("Rating request is required.");
        }

        if (request.Value is < 1 or > 5)
        {
            throw new ValidationException(
                "Rating value must be between 1 and 5.");
        }

        if (!string.IsNullOrWhiteSpace(request.Comment) &&
            request.Comment.Trim().Length > MaximumCommentLength)
        {
            throw new ValidationException(
                $"Rating comment cannot exceed {MaximumCommentLength} characters.");
        }
    }

    private static string? NormalizeComment(string? comment)
    {
        return string.IsNullOrWhiteSpace(comment)
            ? null
            : comment.Trim();
    }

    private static bool IsDuplicateRating(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState ==
                    PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }

    private static TripRatingResponseDto Map(TripRating rating)
    {
        return new TripRatingResponseDto
        {
            Id = rating.Id,
            BookingId = rating.BookingId,
            RaterId = rating.RaterId,
            RatedUserId = rating.RatedUserId,
            Value = rating.Value,
            Comment = rating.Comment,
            CreatedAt = rating.CreatedAt,
            NextAction = WorkflowNextAction.None,
            RequiredActor = WorkflowActor.None
        };
    }

    private static AdminUserRatingResponseDto MapAdmin(
        AdminTripRatingData rating)
    {
        return new AdminUserRatingResponseDto
        {
            RatingId = rating.RatingId,
            BookingId = rating.BookingId,
            TripId = rating.TripId,
            Value = rating.Value,
            Comment = rating.Comment,
            RaterUserId = rating.RaterUserId,
            RaterName = rating.RaterName,
            RaterRole = rating.RaterRole,
            RatedUserId = rating.RatedUserId,
            TripOrigin = rating.TripOrigin,
            TripDestination = rating.TripDestination,
            CreatedAt = rating.CreatedAt
        };
    }
}
