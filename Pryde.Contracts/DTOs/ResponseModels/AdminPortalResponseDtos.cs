using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class StaffResponseDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StaffSummaryResponseDto
{
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public int InactiveStaff { get; set; }
    public int PendingInvites { get; set; }
}

public class StaffListResponseDto : PagedResponseDto<StaffResponseDto>
{
    public StaffSummaryResponseDto Summary { get; set; } = new();
}

public class AdminUserDetailResponseDto : UserSummaryResponseDto
{
    public string FullName { get; set; } = string.Empty;
    public KycVerificationResponseDto? Kyc { get; set; }
}

public class DriverTripSummaryResponseDto
{
    public int TotalTrips { get; set; }
    public int ScheduledTrips { get; set; }
    public int CompletedTrips { get; set; }
}

public class AdminDriverDetailResponseDto : AdminUserDetailResponseDto
{
    public IReadOnlyList<AdminVehicleResponseDto> Vehicles { get; set; } = [];
    public string VehicleDocumentStatus { get; set; } = "NotSubmitted";
    public DriverTripSummaryResponseDto TripSummary { get; set; } = new();
}

public class RecentDriverRequestResponseDto
{
    public Guid DriverId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string KycStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class RevenueSummaryItemResponseDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
}

public class AdminDashboardResponseDto
{
    public int TotalUsers { get; set; }
    public int TotalDrivers { get; set; }
    public int ActiveDrivers { get; set; }
    public int PendingDriverRequests { get; set; }
    public int PendingKycRequests { get; set; }
    public int PendingVehicleDocumentRequests { get; set; }
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public int PendingInvites { get; set; }
    public decimal MonthlyPlatformEarnings { get; set; }
    public decimal TotalPlatformEarnings { get; set; }
    public int TotalTransactions { get; set; }
    public IReadOnlyList<RecentDriverRequestResponseDto> RecentDriverRequests { get; set; } = [];
    public IReadOnlyList<AdminWalletTransactionResponseDto> RecentTransactions { get; set; } = [];
    public IReadOnlyList<RevenueSummaryItemResponseDto> RevenueSummary { get; set; } = [];
}

public class AdminWalletTransactionResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public string Status { get; set; } = "Completed";
    public string? Reference { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTime CreatedAt { get; set; }
}
