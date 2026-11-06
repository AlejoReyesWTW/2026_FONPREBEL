using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>Tabla <c>BatchRecipient</c> tal como la define el §3.4 del diseño de datos.</summary>
public sealed class BatchRecipientEntityConfiguration : IEntityTypeConfiguration<BatchRecipient>
{
    public void Configure(EntityTypeBuilder<BatchRecipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BatchRecipient");
        builder.HasKey(recipient => recipient.Id);

        builder.Property(recipient => recipient.BatchId).IsRequired();
        builder.Property(recipient => recipient.Cedula).IsRequired();
        builder.Property(recipient => recipient.PdfStatus)
            .HasConversion(EnumTextConversion.For<PdfStatus>())
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(recipient => recipient.SendStatus)
            .HasConversion(EnumTextConversion.For<SendStatus>())
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(recipient => recipient.SendAttemptCount).IsRequired();
        builder.Property(recipient => recipient.UpdatedAtUtc).IsRequired();

        builder.HasIndex(recipient => new { recipient.BatchId, recipient.Cedula }).IsUnique();
        builder.HasIndex(recipient => new { recipient.BatchId, recipient.SendStatus });
        builder.HasIndex(recipient => new { recipient.BatchId, recipient.PdfStatus });

        builder.HasOne(recipient => recipient.Batch)
            .WithMany(batch => batch.Recipients)
            .HasForeignKey(recipient => recipient.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
