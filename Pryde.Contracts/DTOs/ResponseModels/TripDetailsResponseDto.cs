namespace Pryde.Contracts.ResponseModels;

public class TripDetailsResponseDto : TripSummaryResponseDto
{
    public int PendingBookingCount { get; set; }
    public int ApprovedBookingCount { get; set; }
}
