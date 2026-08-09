using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reference).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ExternalProvider).HasMaxLength(50);
        builder.Property(x => x.ExternalReference).HasMaxLength(100);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.ExternalReference)
            .IsUnique()
            .HasFilter("\"ExternalReference\" IS NOT NULL");
        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => x.EscrowId);
        builder.HasOne(x => x.Escrow)
            .WithMany()
            .HasForeignKey(x => x.EscrowId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
