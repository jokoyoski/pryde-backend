using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class KycVerification : BaseEntity
    {
        public Guid UserId { get; set; }
        public string? BiometricVerificationUrl { get; set; }
        public string? DriverLicenseUrl { get; set; }
        public string? SecondaryIdentificationUrl { get; set; }
        public KycStatus Status { get; set; } = KycStatus.Pending;
        public DateTime? VerifiedAt { get; set; }
        public User User { get; set; } = null!;
    }
}
