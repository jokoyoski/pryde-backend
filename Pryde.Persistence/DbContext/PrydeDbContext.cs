using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Common;
using Pryde.Domain.Entities;
using System.Reflection;

namespace Pryde.Persistence.Context;

public class PrydeDbContext(DbContextOptions<PrydeDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<KycVerification> KycVerifications { get; set; }
    public DbSet<KycVerificationAttempt> KycVerificationAttempts { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<VehicleDocument> VehicleDocuments { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<PasswordResetCode> PasswordResetCodes { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripBooking> TripBookings { get; set; }
    public DbSet<BookingChat> BookingChats { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<RecurringTrip> RecurringTrips { get; set; }
    public DbSet<TripSubscription> TripSubscriptions { get; set; }
    public DbSet<SavedRecurringTrip> SavedRecurringTrips { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
    public DbSet<PaystackWalletFundingRequest> PaystackWalletFundingRequests { get; set; }
    public DbSet<VehicleImage> VehicleImages { get; set; }
    public DbSet<VehicleAmenity> VehicleAmenities { get; set; }
    public DbSet<Escrow> Escrows { get; set; }
    public DbSet<LedgerAccount> LedgerAccounts { get; set; }
    public DbSet<LedgerTransaction> LedgerTransactions { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }
    public DbSet<DriverBankAccount> DriverBankAccounts { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<TripRating> TripRatings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditInformation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;

        if (ChangeTracker.Entries<LedgerEntry>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Posted ledger entries are immutable.");
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
