using Pryde.Domain.Common;

using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class VehicleImage : BaseEntity
    {
        public Guid VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
        public string ImageUrl { get; set; } = string.Empty;
        public VehicleImageType? ImageType { get; set; }
        public bool IsPrimary { get; set; }
    }
}
