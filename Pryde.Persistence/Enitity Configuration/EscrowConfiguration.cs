using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class EscrowConfiguration : IEntityTypeConfiguration<Escrow>
{
    public void Configure(EntityTypeBuilder<Escrow> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.DriverAmount).HasPrecision(18, 2);
        builder.Property(x => x.PlatformAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasIndex(x => x.BookingId).IsUnique();
        builder.HasOne(x => x.Booking)
            .WithOne(x => x.Escrow)
            .HasForeignKey<Escrow>(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
