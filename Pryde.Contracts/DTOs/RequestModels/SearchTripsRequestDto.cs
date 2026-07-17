namespace Pryde.Contracts.RequestModels;

public class SearchTripsRequestDto
{
    public double? OriginLatitude { get; set; }
    public double? OriginLongitude { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }
    public DateTime? DepartureDate { get; set; }
    public bool? RequiresLuggage { get; set; }
    public int? RequiredSeats { get; set; }
    public double? PickupRadiusKm { get; set; }
}
