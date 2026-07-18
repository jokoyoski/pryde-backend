using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public abstract class PaginationRequestDto
{
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber { get => _pageNumber; set => _pageNumber = Math.Max(1, value); }
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 100); }
}

public class AdminUsersRequestDto : PaginationRequestDto
{
    public string? Role { get; set; }
    public UserStatus? Status { get; set; }
    public string? Search { get; set; }
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
    public bool? IsActive { get; set; }
    public Guid? OwnerId { get; set; }
    public string? Search { get; set; }
}

public class AdminVehicleDocumentsRequestDto : PaginationRequestDto
{
    public Guid? VehicleId { get; set; }
    public Guid? OwnerId { get; set; }
    public VehicleDocumentType? DocumentType { get; set; }
}
