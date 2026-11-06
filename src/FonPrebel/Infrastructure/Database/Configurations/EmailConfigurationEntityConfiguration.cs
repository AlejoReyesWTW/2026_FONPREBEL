using FonPrebel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// Tabla <c>EmailConfiguration</c> tal como la define el §3.5 del diseño de datos: una sola fila.
/// </summary>
public sealed class EmailConfigurationEntityConfiguration : IEntityTypeConfiguration<EmailConfiguration>
{
    /// <summary>Nombre de la restricción que limita la tabla a la fila <c>Id = 1</c>.</summary>
    public const string SingleRowCheckName = "CK_EmailConfiguration_SingleRow";

    /// <summary>
    /// Expresión de la restricción. Es metadato del esquema que EF vuelca en la migración, no una
    /// consulta de negocio: S3 prohíbe el SQL escrito a mano en la aplicación.
    /// </summary>
    private const string SingleRowCheckExpression = "\"Id\" = 1";

    public void Configure(EntityTypeBuilder<EmailConfiguration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailConfiguration", table => table.HasCheckConstraint(SingleRowCheckName, SingleRowCheckExpression));
        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.Id).ValueGeneratedNever();

        builder.Property(configuration => configuration.Host);
        builder.Property(configuration => configuration.Port);
        builder.Property(configuration => configuration.Security);
        builder.Property(configuration => configuration.Account);
        builder.Property(configuration => configuration.SenderAddress);
        builder.Property(configuration => configuration.PasswordCipher);
        builder.Property(configuration => configuration.PasswordUpdatedAtUtc);
        builder.Property(configuration => configuration.UpdatedAtUtc).IsRequired();
    }
}
