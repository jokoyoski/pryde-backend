using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.EntityConfiguration;

public class ChatMessageConfiguration
    : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.MessageText)
            .HasMaxLength(2000)
            .IsRequired();
        builder.HasIndex(message => new
        {
            message.ChatId,
            message.SentAt,
            message.Id
        });
        builder.HasIndex(message => new
        {
            message.ChatId,
            message.SenderId,
            message.ClientMessageId
        }).IsUnique();

        builder.HasOne(message => message.Chat)
            .WithMany(chat => chat.Messages)
            .HasForeignKey(message => message.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
