using FonPrebel.Application.Interfaces;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// Sonda de mentira para las pruebas del monitor: cuenta las llamadas y permite devolver un resultado
/// preparado o lanzar una excepción, sin tocar la red ni el sistema de archivos.
/// </summary>
internal sealed class FakeConnectionProbe : IConnectionProbe
{
    private readonly Func<CancellationToken, Task<ConnectionProbeResult>>? _behavior;
    private int _calls;

    public FakeConnectionProbe(
        string targetKey,
        string targetName,
        Func<CancellationToken, Task<ConnectionProbeResult>>? behavior = null)
    {
        TargetKey = targetKey;
        TargetName = targetName;
        _behavior = behavior;
    }

    public string TargetKey { get; }

    public string TargetName { get; }

    /// <summary>Cantidad de veces que el monitor pidió una comprobación.</summary>
    public int Calls => Volatile.Read(ref _calls);

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        int call = Interlocked.Increment(ref _calls);

        if (_behavior is not null)
        {
            return _behavior(cancellationToken);
        }

        return Task.FromResult(ProbeResults.Online(TargetKey, TargetName, call));
    }

    /// <summary>Sonda que responde siempre con la duración indicada.</summary>
    public static FakeConnectionProbe Responding(string targetKey, string targetName, long durationMilliseconds) =>
        new(
            targetKey,
            targetName,
            _ => Task.FromResult(ProbeResults.Online(targetKey, targetName, call: 1, durationMilliseconds)));
}

/// <summary>Resultados de prueba con momentos crecientes, para poder comprobar el orden del mini-log.</summary>
internal static class ProbeResults
{
    private static readonly DateTimeOffset Origin = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Respuesta verde con la duración y el número de intento indicados.</summary>
    public static ConnectionProbeResult Online(
        string targetKey,
        string targetName,
        int call,
        long durationMilliseconds = 5) =>
        ConnectionProbeResult.Online(
            targetKey,
            targetName,
            Moment(call),
            durationMilliseconds,
            $"Responde (intento {call}).");

    /// <summary>Momento del intento; crece con el número de intento para ordenar el mini-log.</summary>
    public static DateTimeOffset Moment(int call) => Origin.AddSeconds(call);
}

/// <summary>
/// Espera del rechequeo controlada por la prueba: registra cada intervalo pedido y no deja avanzar al
/// ciclo siguiente hasta que la prueba lo libera, así ninguna prueba espera minutos reales.
/// </summary>
internal sealed class StepConnectionMonitorDelay : IConnectionMonitorDelay, IDisposable
{
    private readonly SemaphoreSlim _steps = new(0);
    private readonly object _sync = new();
    private readonly List<TimeSpan> _requested = new();

    /// <summary>Intervalos pedidos por el monitor, en orden.</summary>
    public IReadOnlyList<TimeSpan> Requested
    {
        get
        {
            lock (_sync)
            {
                return _requested.ToArray();
            }
        }
    }

    public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _requested.Add(interval);
        }

        return _steps.WaitAsync(cancellationToken);
    }

    /// <summary>Deja avanzar al ciclo siguiente.</summary>
    public void Release() => _steps.Release();

    public void Dispose() => _steps.Dispose();

    /// <summary>Espera acotada hasta que la condición se cumpla.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition(), $"La condición no se cumplió en {timeout.TotalSeconds} segundos.");
    }
}
