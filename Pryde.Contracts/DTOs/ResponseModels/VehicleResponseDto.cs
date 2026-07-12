namespace Pryde.Contracts.ResponseModels;
public class VehicleResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string LicensePlateNumber { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
}