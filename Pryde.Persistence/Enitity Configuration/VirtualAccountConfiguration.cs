using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class VirtualAccountConfiguration : IEntityTypeConfiguration<VirtualAccount>
{
    public void Configure(EntityTypeBuilder<VirtualAccount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BankName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AccountName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountNumber)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(x => x.AccountNumber)
            .IsUnique();

        builder.HasIndex(x => x.WalletId)
            .IsUnique();

        builder.HasOne(x => x.Wallet)
            .WithOne(x => x.VirtualAccount)
            .HasForeignKey<VirtualAccount>(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
