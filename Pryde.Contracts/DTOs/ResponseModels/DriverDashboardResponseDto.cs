namespace Pryde.Contracts.ResponseModels;

public class DriverDashboardResponseDto
{
    public ProfileResponseDto DriverProfile { get; set; } = new();
    public decimal WalletBalance { get; set; }
    public decimal TodayEarnings { get; set; }
    public decimal TotalEarnings { get; set; }
    public TripSummaryResponseDto? UpcomingTrip { get; set; }
    public IReadOnlyList<TripSummaryResponseDto> RecentTrips { get; set; } = [];
    public int PendingBookingRequestCount { get; set; }
}
