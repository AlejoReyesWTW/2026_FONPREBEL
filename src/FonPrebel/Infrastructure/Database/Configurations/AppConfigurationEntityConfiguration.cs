using FonPrebel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// Tabla <c>AppConfiguration</c> tal como la define el §3.6 del diseño de datos: un parámetro por fila
/// y un valor nulo que significa "pendiente de configurar".
/// </summary>
public sealed class AppConfigurationEntityConfiguration : IEntityTypeConfiguration<AppConfiguration>
{
    public void Configure(EntityTypeBuilder<AppConfiguration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AppConfiguration");
        builder.HasKey(configuration => configuration.Key);

        builder.Property(configuration => configuration.Key).IsRequired();
        builder.Property(configuration => configuration.Value);
        builder.Property(configuration => configuration.UpdatedAtUtc).IsRequired();
        builder.Property(configuration => configuration.UpdatedBy);
    }
}
