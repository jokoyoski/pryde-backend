using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using System.Reflection;
using System.Reflection.Emit;

namespace Pryde.Persistence.Context;

public class PrydeDbContext(DbContextOptions<PrydeDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }

    public DbSet<Profile> Profiles { get; set; }

    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

    public DbSet<KycVerification> KycVerifications { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }

    public DbSet<VehicleDocument> VehicleDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
    }
}