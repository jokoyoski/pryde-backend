using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class VehicleImage : BaseEntity
    {
        public Guid VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}