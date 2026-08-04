using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.Configurations;

public class TripRatingConfiguration : IEntityTypeConfiguration<TripRating>
{
    public void Configure(EntityTypeBuilder<TripRating> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Comment)
            .HasMaxLength(1000);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TripRatings_Value",
            "\"Value\" >= 1 AND \"Value\" <= 5"));

        builder.HasOne(x => x.Booking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Rater)
            .WithMany()
            .HasForeignKey(x => x.RaterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RatedUser)
            .WithMany()
            .HasForeignKey(x => x.RatedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BookingId, x.RaterId })
            .IsUnique();

        builder.HasIndex(x => x.RatedUserId);
    }
}
