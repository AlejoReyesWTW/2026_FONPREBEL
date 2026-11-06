using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>Tabla <c>BatchExecution</c> tal como la define el §3.2 del diseño de datos.</summary>
public sealed class BatchExecutionEntityConfiguration : IEntityTypeConfiguration<BatchExecution>
{
    public void Configure(EntityTypeBuilder<BatchExecution> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BatchExecution");
        builder.HasKey(execution => execution.Id);

        builder.Property(execution => execution.BatchId).IsRequired();
        builder.Property(execution => execution.Number).IsRequired();
        builder.Property(execution => execution.Status)
            .HasConversion(EnumTextConversion.For<BatchStatus>())
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(execution => execution.StartedAtUtc).IsRequired();

        builder.HasIndex(execution => new { execution.BatchId, execution.Number }).IsUnique();

        // El historial no se borra encadenado desde el lote (§2.8 reglas 8 y 9).
        builder.HasOne(execution => execution.Batch)
            .WithMany(batch => batch.Executions)
            .HasForeignKey(execution => execution.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(execution => execution.ResumedFromExecution)
            .WithMany()
            .HasForeignKey(execution => execution.ResumedFromExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
