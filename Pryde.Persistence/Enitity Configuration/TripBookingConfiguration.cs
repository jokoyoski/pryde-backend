using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class TripBookingConfiguration : IEntityTypeConfiguration<TripBooking>
{
    public void Configure(EntityTypeBuilder<TripBooking> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SeatPrice).HasPrecision(18, 2);
        builder.Property(x => x.ServiceCharge).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.Trip)
            .WithMany(t => t.Bookings)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Passenger)
            . WithMany()
            .HasForeignKey(x => x.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TripId, x.PassengerId })
            .IsUnique()
            .HasFilter("\"Status\" IN (1, 2)");
    }
}
