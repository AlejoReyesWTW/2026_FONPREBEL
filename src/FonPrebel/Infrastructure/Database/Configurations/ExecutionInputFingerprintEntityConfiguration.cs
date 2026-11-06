using FonPrebel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// Tabla <c>ExecutionInputFingerprint</c> tal como la define el §3.7 del diseño de datos.
/// </summary>
public sealed class ExecutionInputFingerprintEntityConfiguration : IEntityTypeConfiguration<ExecutionInputFingerprint>
{
    public void Configure(EntityTypeBuilder<ExecutionInputFingerprint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExecutionInputFingerprint");
        builder.HasKey(fingerprint => fingerprint.Id);

        builder.Property(fingerprint => fingerprint.BatchExecutionId).IsRequired();
        builder.Property(fingerprint => fingerprint.Role).IsRequired();
        builder.Property(fingerprint => fingerprint.FilePath).IsRequired();
        builder.Property(fingerprint => fingerprint.FileSizeBytes).IsRequired();
        builder.Property(fingerprint => fingerprint.FileModifiedAtUtc).IsRequired();
        builder.Property(fingerprint => fingerprint.Sha256).IsRequired();
        builder.Property(fingerprint => fingerprint.CapturedAtUtc).IsRequired();

        builder.HasIndex(fingerprint => new { fingerprint.BatchExecutionId, fingerprint.Role });

        builder.HasOne(fingerprint => fingerprint.BatchExecution)
            .WithMany(execution => execution.InputFingerprints)
            .HasForeignKey(fingerprint => fingerprint.BatchExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
