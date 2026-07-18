using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class AdminKycResponseDto : KycVerificationResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}

public class AdminVehicleResponseDto : VehicleResponseDto
{
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public IReadOnlyList<VehicleDocumentResponseDto> Documents { get; set; } = [];
}

public class AdminVehicleDocumentResponseDto : VehicleDocumentResponseDto
{
    public Guid OwnerId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string LicensePlateNumber { get; set; } = string.Empty;
}
