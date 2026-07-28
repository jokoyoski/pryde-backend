using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(250);
        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.BankName).HasMaxLength(150);
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(20);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId);
        builder.HasIndex(x => new { x.WalletId, x.Type, x.CreatedAt });
    }
}
