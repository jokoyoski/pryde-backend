using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pryde.Domain.Entities
{
    public class Vehicle
    {
        public Guid UserId { get; set; }

        public string LicensePlateNumber { get; set; } = string.Empty;

        public string VehicleImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public int Capacity { get; set; }

        public User User { get; set; } = null!;

        public ICollection<VehicleDocument> Documents { get; set; }
            = new List<VehicleDocument>();
    }
}
