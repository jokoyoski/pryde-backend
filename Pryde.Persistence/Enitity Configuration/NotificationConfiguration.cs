using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.EntityConfigurations;

public class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(
        EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.UserId)
            .IsRequired();
        builder.Property(notification => notification.Type)
            .IsRequired();
        builder.Property(notification => notification.Title)
            .IsRequired()
            .HasMaxLength(150);
        builder.Property(notification => notification.Message)
            .IsRequired()
            .HasMaxLength(1000);
        builder.Property(notification =>
                notification.RelatedEntityType)
            .HasMaxLength(100);
        builder.Property(notification => notification.Action)
            .HasMaxLength(100);
        builder.Property(notification =>
                notification.DeduplicationKey)
            .HasMaxLength(200);
        builder.Property(notification => notification.IsRead)
            .HasDefaultValue(false);

        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.CreatedAt
        });
        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.IsRead,
            notification.CreatedAt
        });
        builder.HasIndex(notification => new
        {
            notification.Type,
            notification.CreatedAt
        });
        builder.HasIndex(notification =>
                notification.DeduplicationKey)
            .IsUnique()
            .HasFilter(
                "\"DeduplicationKey\" IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
