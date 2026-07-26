using Microsoft.AspNetCore.Http;
using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public sealed class VehicleDetailsRequestDto
{
    public string VehicleOwnerName { get; init; } = string.Empty;
    public VehicleRegistrationType RegistrationType { get; init; }
    public string VehicleType { get; init; } = string.Empty;
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int ManufacturingYear { get; init; }
    public string Colour { get; init; } = string.Empty;
}

public sealed class VehicleMediaRequestDto
{
    public IFormFile? FrontView { get; init; }
    public IFormFile? RearView { get; init; }
    public IFormFile? SideProfile { get; init; }
    public IFormFile? Interior { get; init; }
    public IFormFile? WalkAroundVideo { get; init; }
}

public sealed class VehicleCapacityExtrasRequestDto
{
    public int PassengerSeatCount { get; init; }
    public LuggageCapacity? LuggageCapacity { get; init; }
    public IReadOnlyCollection<VehicleAmenityType> Amenities { get; init; } = [];
    public string? AdditionalDetails { get; init; }
}
