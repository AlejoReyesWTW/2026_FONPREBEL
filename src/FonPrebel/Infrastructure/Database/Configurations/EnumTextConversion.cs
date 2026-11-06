using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// §1 del diseño de datos: las enumeraciones se guardan como <c>TEXT</c> con el nombre del valor para
/// que la base sea legible al revisarla.
/// </summary>
internal static class EnumTextConversion
{
    /// <summary>Convierte una enumeración a su nombre y de vuelta al valor tipado.</summary>
    public static ValueConverter<TEnum, string> For<TEnum>()
        where TEnum : struct, Enum =>
        new(status => status.ToString(), text => Enum.Parse<TEnum>(text));
}
