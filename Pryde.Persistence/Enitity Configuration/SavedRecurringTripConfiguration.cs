using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class SavedRecurringTripConfiguration
    : IEntityTypeConfiguration<SavedRecurringTrip>
{
    public void Configure(EntityTypeBuilder<SavedRecurringTrip> builder)
    {
        builder.HasKey(item => item.Id);

        builder.HasOne(item => item.RecurringTrip)
            .WithMany(schedule => schedule.SavedByPassengers)
            .HasForeignKey(item => item.RecurringTripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Passenger)
            .WithMany()
            .HasForeignKey(item => item.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new
        {
            item.RecurringTripId,
            item.PassengerId
        })
            .IsUnique();

        builder.HasIndex(item => new
        {
            item.PassengerId,
            item.CreatedAt
        });
    }
}
