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

        public int Capacity { get; set; }

        public string? InsuranceDocumentUrl { get; set; }

        public DateTime? InsuranceExpiryDate { get; set; }

        public User User { get; set; } = null!;
    }
}
