using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.Configurations;

public class DriverBankAccountConfiguration
    : IEntityTypeConfiguration<DriverBankAccount>
{
    public void Configure(EntityTypeBuilder<DriverBankAccount> builder)
    {
        builder.HasKey(bankAccount => bankAccount.Id);

        builder.Property(bankAccount => bankAccount.BankCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(bankAccount => bankAccount.BankName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(bankAccount => bankAccount.AccountNumber)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(bankAccount => bankAccount.AccountName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(bankAccount => bankAccount.RecipientCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(bankAccount => new
            {
                bankAccount.UserId,
                bankAccount.BankCode,
                bankAccount.AccountNumber
            })
            .IsUnique();

        builder.HasOne(bankAccount => bankAccount.User)
            .WithMany(user => user.DriverBankAccounts)
            .HasForeignKey(bankAccount => bankAccount.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
