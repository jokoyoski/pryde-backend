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
        public string? ProviderName { get; set; }
        public string? ProviderReference { get; set; }
        public string? DojahReference { get; set; }
        public string? ProviderStatus { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? LastProviderUpdatedAt { get; set; }
        public User User { get; set; } = null!;
    }
}
