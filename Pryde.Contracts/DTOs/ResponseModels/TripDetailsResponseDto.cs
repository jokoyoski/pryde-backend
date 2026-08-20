namespace Pryde.Contracts.ResponseModels;

public class TripDetailsResponseDto : TripSummaryResponseDto
{
    public int PendingBookingCount { get; set; }
    public int ApprovedBookingCount { get; set; }
}

public class CustomerTripDetailsResponseDto : TripDetailsResponseDto
{
    public DriverSummaryDto Driver { get; set; } = new();
    public VehicleSummaryDto Vehicle { get; set; } = new();
}

public class DriverSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public double AverageRating { get; set; }
}

public class VehicleSummaryDto
{
    public Guid Id { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Color { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public int Capacity { get; set; }
}
