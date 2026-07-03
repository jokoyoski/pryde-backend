namespace Pryde.Contracts.ResponseModels;
public class VehicleResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string LicensePlateNumber { get; set; } = string.Empty;
    public string VehicleImageUrl { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
}