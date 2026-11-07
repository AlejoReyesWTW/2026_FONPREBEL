using Microsoft.Extensions.Logging;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// Logger de prueba que guarda los mensajes emitidos, para verificar avisos que deben aparecer una sola vez
/// (por ejemplo, el formato provisional del identificador de lote).
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<Entry> _entries = new();
    private readonly object _sync = new();

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        lock (_sync)
        {
            _entries.Add(new Entry(logLevel, formatter(state, exception)));
        }
    }

    /// <summary>Entrada registrada: nivel y mensaje.</summary>
    internal sealed record Entry(LogLevel Level, string Message);
}
