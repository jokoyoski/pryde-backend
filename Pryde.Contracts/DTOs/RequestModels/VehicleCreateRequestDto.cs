using Microsoft.AspNetCore.Http;

namespace Pryde.Contracts.RequestModels;

public sealed class VehicleCreateRequestDto
{
    public string LicensePlateNumber { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public IFormFile VehicleImage { get; init; } = default!;
}