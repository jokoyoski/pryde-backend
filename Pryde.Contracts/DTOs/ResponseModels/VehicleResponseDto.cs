namespace Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;

public class VehicleResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string LicensePlateNumber { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public List<VehicleImageResponseDto> Images { get; set; } = [];
    public string? VehicleOwnerName { get; set; }
    public VehicleRegistrationType? RegistrationType { get; set; }
    public string? VehicleType { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public string? Colour { get; set; }
    public string? WalkAroundVideoUrl { get; set; }
    public int? PassengerSeatCount { get; set; }
    public LuggageCapacity? LuggageCapacity { get; set; }
    public IReadOnlyList<VehicleAmenityType> Amenities { get; set; } = [];
    public string? AdditionalDetails { get; set; }
    public VehicleOnboardingStatus OnboardingStatus { get; set; }
    public string? RejectionReason { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
}

public class VehicleImageResponseDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public VehicleImageType? ImageType { get; set; }
    public bool IsPrimary { get; set; }
}
