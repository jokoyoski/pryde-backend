namespace Pryde.Contracts.ResponseModels;

public class DriverDashboardResponseDto
{
    public ProfileResponseDto DriverProfile { get; set; } = new();
    public decimal WalletBalance { get; set; }
    public decimal TodayEarnings { get; set; }
    public decimal ThisWeekEarnings { get; set; }
    public decimal TotalEarnings { get; set; }
    public int CompletedTripCount { get; set; }
    public DriverDashboardTripSummaryResponseDto? UpcomingTrip { get; set; }
    public IReadOnlyList<DriverDashboardTripSummaryResponseDto> RecentTrips { get; set; } = [];
    public int PendingBookingRequestCount { get; set; }
}
