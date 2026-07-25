using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class VehicleAmenityConfiguration : IEntityTypeConfiguration<VehicleAmenity>
{
    public void Configure(EntityTypeBuilder<VehicleAmenity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AmenityType)
            .IsRequired();

        builder.HasIndex(x => new { x.VehicleId, x.AmenityType })
            .IsUnique();

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Amenities)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
