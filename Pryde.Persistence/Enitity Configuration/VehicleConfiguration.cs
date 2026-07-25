using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LicensePlateNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Capacity)
            .IsRequired();

        builder.Property(x => x.VehicleOwnerName)
            .HasMaxLength(200);

        builder.Property(x => x.WalkAroundVideoUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.AdditionalDetails)
            .HasMaxLength(1000);

        builder.Property(x => x.OnboardingStatus)
            .IsRequired();
    }
}
