using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace FonPrebel.Infrastructure.Logging;

/// <summary>
/// Formato de una línea de bitácora (S17): fecha y hora, nivel, mensaje y, sólo si existen, las
/// propiedades <c>Lote</c>, <c>Ejecucion</c>, <c>Fase</c> y <c>Resultado</c>. Una línea sin esas
/// propiedades no deja separadores vacíos.
/// </summary>
public sealed class FonPrebelLogLineFormatter : ITextFormatter
{
    /// <summary>Separador entre los campos de la línea.</summary>
    public const string Separator = " | ";

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(logEvent.Timestamp.LocalDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        output.Write(Separator);
        output.Write(RenderLevel(logEvent.Level));
        output.Write(Separator);
        output.Write(logEvent.RenderMessage(CultureInfo.InvariantCulture));

        foreach (string propertyName in LogPropertyNames.Ordered)
        {
            if (logEvent.Properties.TryGetValue(propertyName, out LogEventPropertyValue? value))
            {
                output.Write(Separator);
                output.Write(propertyName);
                output.Write(": ");
                output.Write(RenderValue(value));
            }
        }

        output.WriteLine();

        if (logEvent.Exception is not null)
        {
            output.WriteLine(logEvent.Exception.ToString());
        }
    }

    private static string RenderLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "DETALLE",
        LogEventLevel.Debug => "DEPURA",
        LogEventLevel.Information => "INFO",
        LogEventLevel.Warning => "AVISO",
        LogEventLevel.Error => "ERROR",
        LogEventLevel.Fatal => "CRITICO",
        _ => level.ToString().ToUpperInvariant(),
    };

    private static string RenderValue(LogEventPropertyValue value) =>
        value is ScalarValue { Value: string text } ? text : value.ToString();
}
