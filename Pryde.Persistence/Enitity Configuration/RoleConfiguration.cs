using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly DateTime SeedDate =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.HasData(
            new Role
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "SuperAdmin",
                CreatedAt = SeedDate
            },
            new Role
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Admin",
                CreatedAt = SeedDate
            },
            new Role
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Driver",
                CreatedAt = SeedDate
            },
            new Role
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Passenger",
                CreatedAt = SeedDate
            }
        );
    }
}