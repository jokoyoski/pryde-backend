using Pryde.Domain.Common;

using Pryde.Domain.Enums;

namespace Pryde.Domain.Entities
{
    public class Vehicle : BaseEntity
    {
        public Guid UserId { get; set; }
        public string LicensePlateNumber { get; set; } = string.Empty;
        public string? VehicleOwnerName { get; set; }
        public VehicleRegistrationType? RegistrationType { get; set; }
        public string? VehicleType { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? ManufacturingYear { get; set; }
        public string? Colour { get; set; }
        public string? WalkAroundVideoUrl { get; set; }
        public int? PassengerSeatCount { get; set; }
        public LuggageCapacity? LuggageCapacity { get; set; }
        public string? AdditionalDetails { get; set; }
        public VehicleOnboardingStatus OnboardingStatus { get; set; } =
            VehicleOnboardingStatus.Draft;
        public string? RejectionReason { get; set; }
        public bool IsActive { get; set; }
        public int Capacity { get; set; }
        public User User { get; set; } = null!;
        public ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();
        public ICollection<VehicleImage> Images { get; set; } = new List<VehicleImage>();
        public ICollection<VehicleAmenity> Amenities { get; set; } = new List<VehicleAmenity>();
    }
}
