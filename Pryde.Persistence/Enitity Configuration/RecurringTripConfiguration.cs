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
            .OnDelete(DeleteBehavior
            .Restrict);
    }
}