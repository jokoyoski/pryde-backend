using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;

public class KycVerificationConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.ProviderName).HasMaxLength(50);
        builder.Property(x => x.ProviderReference).HasMaxLength(100);
        builder.Property(x => x.ProviderStatus).HasMaxLength(50);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.HasIndex(x => x.ProviderReference)
            .IsUnique()
            .HasFilter("\"ProviderReference\" IS NOT NULL");
    }
}
