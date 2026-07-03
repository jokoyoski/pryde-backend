using Microsoft.AspNetCore.Http;
namespace Pryde.Contracts.RequestModels;
public class VehicleUpdateRequestDto
{
    public int Capacity { get; set; }
    public IFormFile? VehicleImage { get; init; }
}