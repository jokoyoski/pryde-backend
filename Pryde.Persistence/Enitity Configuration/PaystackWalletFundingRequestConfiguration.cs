using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

namespace Pryde.Persistence.Configurations;

public class PaystackWalletFundingRequestConfiguration
    : IEntityTypeConfiguration<PaystackWalletFundingRequest>
{
    public void Configure(
        EntityTypeBuilder<PaystackWalletFundingRequest> builder)
    {
        builder.HasKey(fundingRequest => fundingRequest.Id);
        builder.Property(fundingRequest => fundingRequest.Reference)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(fundingRequest => fundingRequest.Currency)
            .IsRequired()
            .HasMaxLength(3);
        builder.Property(fundingRequest => fundingRequest.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);
        builder.HasIndex(fundingRequest => fundingRequest.Reference).IsUnique();
        builder.HasIndex(fundingRequest => fundingRequest.PaystackTransactionId)
            .IsUnique()
            .HasFilter("\"PaystackTransactionId\" IS NOT NULL");
        builder.HasIndex(fundingRequest => new { fundingRequest.UserId, fundingRequest.CreatedAt });
        builder.HasOne(fundingRequest => fundingRequest.User)
            .WithMany()
            .HasForeignKey(fundingRequest => fundingRequest.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
