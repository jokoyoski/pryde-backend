using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.WalletId).IsUnique().HasFilter("\"WalletId\" IS NOT NULL");
        builder.HasOne(x => x.Wallet)
            .WithOne(x => x.LedgerAccount)
            .HasForeignKey<LedgerAccount>(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
