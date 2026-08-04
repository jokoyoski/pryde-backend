namespace Pryde.Contracts.ResponseModels;

public sealed class TripRatingResponseDto : WorkflowResponseDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid RaterId { get; set; }
    public Guid RatedUserId { get; set; }
    public int Value { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserRatingSummaryResponseDto
{
    public Guid UserId { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public sealed class AdminUserRatingResponseDto
{
    public Guid RatingId { get; set; }
    public Guid BookingId { get; set; }
    public Guid TripId { get; set; }
    public int Value { get; set; }
    public string? Comment { get; set; }
    public Guid RaterUserId { get; set; }
    public string RaterName { get; set; } = string.Empty;
    public string RaterRole { get; set; } = string.Empty;
    public Guid RatedUserId { get; set; }
    public string TripOrigin { get; set; } = string.Empty;
    public string TripDestination { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminUserRatingsResponseDto
    : PagedResponseDto<AdminUserRatingResponseDto>
{
    public Guid UserId { get; set; }
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public double RatingPercentage { get; set; }
}
