using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pryde.Domain.Entities;

public class KycVerificationAttemptConfiguration : IEntityTypeConfiguration<KycVerificationAttempt>
{
    public void Configure(EntityTypeBuilder<KycVerificationAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CorrelationReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderReference).HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.RawStatus).HasMaxLength(100);
        builder.Property(x => x.ResultCode).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.ResultText).HasMaxLength(500);
        builder.Property(x => x.FlowType).HasMaxLength(50);
        builder.Property(x => x.AttemptGroupReference).HasMaxLength(100);
        builder.Property(x => x.ExternalUserReference).HasMaxLength(100);
        builder.Property(x => x.CallbackPayloadHash).HasMaxLength(64);
        builder.Property(x => x.VerificationUrl).HasMaxLength(2048);
        builder.Property(x => x.IdentityType).HasMaxLength(50);
        builder.Property(x => x.VerificationMethod).HasMaxLength(50);
        builder.Property(x => x.IdentityOptions).HasMaxLength(500);

        builder.HasIndex(x => new { x.ProviderName, x.CorrelationReference }).IsUnique();
        builder.HasIndex(x => new { x.ProviderName, x.ProviderReference })
            .IsUnique()
            .HasFilter("\"ProviderReference\" IS NOT NULL");

        builder.HasOne(x => x.KycVerification)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.KycVerificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
