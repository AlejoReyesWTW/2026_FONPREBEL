using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FonPrebel.Infrastructure.Database.Configurations;

/// <summary>
/// §1 del diseño de datos: las fechas se guardan en UTC con ISO-8601 en <c>TEXT</c> y se convierten a
/// hora local sólo al mostrarse. Se aplica a todas las propiedades de fecha del modelo.
/// </summary>
internal sealed class UtcDateTimeToTextConverter : ValueConverter<DateTime, string>
{
    private const string RoundTripFormat = "O";

    public UtcDateTimeToTextConverter()
        : base(
            value => value.ToUniversalTime().ToString(RoundTripFormat, CultureInfo.InvariantCulture),
            text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime())
    {
    }
}
