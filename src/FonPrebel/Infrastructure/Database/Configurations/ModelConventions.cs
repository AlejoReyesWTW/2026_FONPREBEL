using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// Convenciones de modelo comunes. Se centralizan acá para que una columna nueva no quede con un
/// formato de fecha distinto al del §1 del diseño de datos.
/// </summary>
internal static class ModelConventions
{
    /// <summary>
    /// Guarda toda fecha en UTC con ISO-8601 en <c>TEXT</c>. Las enumeraciones no se registran acá
    /// porque cada tabla nombra su tipo explícitamente en su propia configuración.
    /// </summary>
    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeToTextConverter>();
    }
}
