using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public abstract class PaginationRequestDto
{
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber { get => _pageNumber; set => _pageNumber = Math.Max(1, value); }
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 100); }
}

public class WalletTransactionsRequestDto : PaginationRequestDto
{
}

public class AdminUsersRequestDto : PaginationRequestDto
{
    public string? Role { get; set; }
    public UserStatus? Status { get; set; }
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsEmailVerified { get; set; }
    public bool? IsPhoneVerified { get; set; }
    public KycStatus? KycStatus { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class AdminKycRequestDto : PaginationRequestDto
{
    public KycStatus? Status { get; set; }
    public string? Role { get; set; }
    public string? Provider { get; set; }
    public string? Search { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class AdminVehiclesRequestDto : PaginationRequestDto
{
    public VehicleOnboardingStatus? OnboardingStatus { get; set; }
    public bool? IsActive { get; set; }
    public Guid? OwnerId { get; set; }
    public VehicleRegistrationType? RegistrationType { get; set; }
    public string? Search { get; set; }
}

public class AdminVehicleDocumentsRequestDto : PaginationRequestDto
{
    public Guid? VehicleId { get; set; }
    public Guid? OwnerId { get; set; }
    public VehicleDocumentType? DocumentType { get; set; }
    public VehicleDocumentReviewStatus? ReviewStatus { get; set; }
    public DateTime? ExpiryFrom { get; set; }
    public DateTime? ExpiryTo { get; set; }
}

public class AdminTripsRequestDto : PaginationRequestDto
{
    public string? Search { get; set; }
    public Guid? DriverId { get; set; }
    public TripStatus? Status { get; set; }
    public DateTime? DepartureFrom { get; set; }
    public DateTime? DepartureTo { get; set; }
    public bool? IsRecurring { get; set; }
    public bool? IsActive { get; set; }
}

public class AdminBookingsRequestDto : PaginationRequestDto
{
    public Guid? UserId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? TripId { get; set; }
    public BookingStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
