using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.HasIndex(x => x.PhoneNumber)
            .IsUnique();

        builder.HasOne(x => x.Profile)
            .WithOne(x => x.User)
            .HasForeignKey<Profile>(x => x.UserId);

        builder.HasOne(x => x.KycVerification)
            .WithOne(x => x.User)
            .HasForeignKey<KycVerification>(x => x.UserId);

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId);

        builder.HasMany(x => x.Vehicles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId);
    }
}
