using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class VehicleImageConfiguration : IEntityTypeConfiguration<VehicleImage>
{
    public void Configure(EntityTypeBuilder<VehicleImage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ImageUrl)
            .IsRequired();

        builder.HasIndex(x => new { x.VehicleId, x.ImageType })
            .IsUnique();

        builder.HasOne(x => x.Vehicle)
            .WithMany(v => v.Images)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
