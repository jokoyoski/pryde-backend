using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.HasOne(x => x.LedgerTransaction)
            .WithMany(x => x.Entries)
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LedgerAccount)
            .WithMany(x => x.Entries)
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
