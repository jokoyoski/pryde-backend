using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class PasswordResetCodeConfiguration : IEntityTypeConfiguration<PasswordResetCode>
{
    public void Configure(EntityTypeBuilder<PasswordResetCode> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.HasIndex(x => x.UserId);
    }
}