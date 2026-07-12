using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class TripSubscriptionConfiguration : IEntityTypeConfiguration<TripSubscription>
{
    public void Configure(EntityTypeBuilder<TripSubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.RecurringTrip)
            .WithMany(r => r.Subscriptions)
            .HasForeignKey(x => x.RecurringTripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Passenger).WithMany()
            .HasForeignKey(x => x.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RecurringTripId, x.PassengerId }).IsUnique();
    }
}