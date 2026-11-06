using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace FonPrebel.Infrastructure.Logging;

/// <summary>
/// Destino de archivo con rotación por día en carpeta <c>YYYY-MM-DD</c> (S17). Serilog.Sinks.File no
/// admite la fecha dentro de la ruta, así que el archivo del día se abre y se cierra cuando cambia
/// la fecha del evento: el resultado es <c>Logs\YYYY-MM-DD\execution.log</c> sin sufijos, y la UI
/// puede leerlo mientras se escribe porque el archivo se abre compartido.
/// </summary>
internal sealed class DailyFileSink : ILogEventSink, IDisposable
{
    private readonly Func<DateOnly, string> _pathForDay;
    private readonly ITextFormatter _formatter;
    private readonly object _sync = new();

    private Logger? _currentLogger;
    private DateOnly? _currentDay;

    public DailyFileSink(Func<DateOnly, string> pathForDay, ITextFormatter formatter)
    {
        _pathForDay = pathForDay ?? throw new ArgumentNullException(nameof(pathForDay));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        DateOnly day = DateOnly.FromDateTime(logEvent.Timestamp.LocalDateTime);

        lock (_sync)
        {
            try
            {
                if (_currentLogger is null || _currentDay != day)
                {
                    OpenDay(day);
                }

                _currentLogger!.Write(logEvent);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Un fallo al escribir la bitácora no puede tumbar la aplicación: se anota en el
                // autoreporte de Serilog y el proceso sigue.
                SelfLog.WriteLine("No se pudo escribir la bitácora del día {0}: {1}", day, exception.Message);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            CloseDay();
        }
    }

    private void OpenDay(DateOnly day)
    {
        CloseDay();

        _currentLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                formatter: _formatter,
                path: _pathForDay(day),
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                buffered: false,
                shared: true)
            .CreateLogger();

        _currentDay = day;
    }

    private void CloseDay()
    {
        _currentLogger?.Dispose();
        _currentLogger = null;
        _currentDay = null;
    }
}
