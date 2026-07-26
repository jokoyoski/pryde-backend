using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pryde.Domain.Common;
using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class VehicleDocument : BaseEntity
    {
        public Guid VehicleId { get; set; }
        public VehicleDocumentType DocumentType { get; set; }
        public string DocumentUrl { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public VehicleDocumentReviewStatus ReviewStatus { get; set; } = VehicleDocumentReviewStatus.Pending;
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
    }
}
