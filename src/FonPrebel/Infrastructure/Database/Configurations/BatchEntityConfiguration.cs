using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>Tabla <c>Batch</c> tal como la define el §3.1 del diseño de datos.</summary>
public sealed class BatchEntityConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Batch");
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.Code).IsRequired();
        builder.Property(batch => batch.PeriodYear).IsRequired();
        builder.Property(batch => batch.PeriodMonth).IsRequired();
        builder.Property(batch => batch.Status)
            .HasConversion(EnumTextConversion.For<BatchStatus>())
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(batch => batch.CreatedAtUtc).IsRequired();
        builder.Property(batch => batch.CreatedBy).IsRequired();
        builder.Property(batch => batch.Notes);

        builder.HasIndex(batch => batch.Code).IsUnique();
        builder.HasIndex(batch => new { batch.PeriodYear, batch.PeriodMonth });
        builder.HasIndex(batch => batch.Status);
    }
}
