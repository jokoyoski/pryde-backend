using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities;

public class VehicleAmenity : BaseEntity
{
    public Guid VehicleId { get; set; }
    public VehicleAmenityType AmenityType { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
}
