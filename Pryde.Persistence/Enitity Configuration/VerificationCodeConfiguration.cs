using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.EntityConfigurations;

public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Purpose).IsRequired();
        builder.Property(code => code.Channel).IsRequired();
        builder.Property(code => code.CodeHash).IsRequired().HasMaxLength(64);
        builder.Property(code => code.ExpiresAt).IsRequired();
        builder.Property(code => code.AttemptCount).IsRequired();
        builder.Property(code => code.LastSentAt).IsRequired();

        builder.HasIndex(code => new
        {
            code.UserId,
            code.Purpose,
            code.Channel,
            code.CreatedAt
        });

        builder.HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
