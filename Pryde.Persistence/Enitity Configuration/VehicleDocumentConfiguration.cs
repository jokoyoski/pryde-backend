using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentUrl)
            .IsRequired();

        builder.Property(x => x.DocumentType)
            .IsRequired();

        builder.Property(x => x.ReviewStatus)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.VehicleId);
    }
}
