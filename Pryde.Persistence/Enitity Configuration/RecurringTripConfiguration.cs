using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class RecurringTripConfiguration : IEntityTypeConfiguration<RecurringTrip>
{
    public void Configure(EntityTypeBuilder<RecurringTrip> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Driver)
            .WithMany().HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.OriginAddress)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.DestinationAddress)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.RoutePolyline)
            .HasMaxLength(10000);

        builder.HasIndex(x => x.DriverId);
        builder.HasIndex(x => new { x.IsActive, x.StartDate, x.EndDate });
    }
}
