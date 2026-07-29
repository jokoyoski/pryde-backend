using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DestinationAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.TripFare)
            .HasPrecision(18, 2);

        builder.Property(x => x.SeatPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.ServiceChargePercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.AvailableSeats)
            .IsConcurrencyToken();

        builder.Property(x => x.Status)
            .IsConcurrencyToken();

        builder.HasOne(x => x.Driver).WithMany()
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RecurringTrip)
            .WithMany(r => r.Trips)
            .HasForeignKey(x => x.RecurringTripId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.DepartureTime);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new
        {
            x.Status,
            x.ConfirmationDeadline
        });
    }
}
