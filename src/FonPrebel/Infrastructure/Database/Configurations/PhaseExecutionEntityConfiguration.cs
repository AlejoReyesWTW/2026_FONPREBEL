using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>Tabla <c>PhaseExecution</c> tal como la define el §3.3 del diseño de datos.</summary>
public sealed class PhaseExecutionEntityConfiguration : IEntityTypeConfiguration<PhaseExecution>
{
    public void Configure(EntityTypeBuilder<PhaseExecution> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PhaseExecution");
        builder.HasKey(phase => phase.Id);

        builder.Property(phase => phase.BatchExecutionId).IsRequired();
        builder.Property(phase => phase.Phase).IsRequired();
        builder.Property(phase => phase.Status)
            .HasConversion(EnumTextConversion.For<PhaseStatus>())
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(phase => phase.ProcessedCount).IsRequired();

        builder.HasIndex(phase => new { phase.BatchExecutionId, phase.Phase }).IsUnique();

        builder.HasOne(phase => phase.BatchExecution)
            .WithMany(execution => execution.Phases)
            .HasForeignKey(phase => phase.BatchExecutionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
