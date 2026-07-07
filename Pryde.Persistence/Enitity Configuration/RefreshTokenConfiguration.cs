using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.UserId);
    }
}