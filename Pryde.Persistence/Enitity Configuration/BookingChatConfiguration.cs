using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.EntityConfiguration;

public class BookingChatConfiguration
    : IEntityTypeConfiguration<BookingChat>
{
    public void Configure(EntityTypeBuilder<BookingChat> builder)
    {
        builder.HasKey(chat => chat.Id);
        builder.HasIndex(chat => chat.BookingId).IsUnique();

        builder.HasOne(chat => chat.Booking)
            .WithOne(booking => booking.Chat)
            .HasForeignKey<BookingChat>(chat => chat.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
