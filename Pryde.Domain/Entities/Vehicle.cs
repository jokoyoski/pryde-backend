using Pryde.Domain.Common;

namespace Pryde.Domain.Entities
{
    public class Vehicle : BaseEntity
    {
        public Guid UserId { get; set; }
        public string LicensePlateNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Capacity { get; set; }
        public User User { get; set; } = null!;
        public ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();
        public ICollection<VehicleImage> Images { get; set; } = new List<VehicleImage>();
    }
}