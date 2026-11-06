using System.Globalization;
using FonPrebel.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// §2.14: rechequeo de las dos conexiones en segundo plano. Corre una vez al iniciar y luego cada
/// <c>Connections.AutoCheckMinutes</c>, que se vuelve a leer en cada ciclo. Guarda el último resultado de
/// cada sonda y un mini-log acotado por sonda, y nunca deja escapar una excepción dentro del bucle.
/// </summary>
/// <remarks>
/// <para>
/// Cada intento —automático o pedido desde la pantalla— pasa por
/// <see cref="ConnectionStatePolicy.ApplySlowThreshold"/> con el umbral vigente, de modo que la decisión
/// de "lenta" tiene un solo dueño. Una sonda rota se traduce a
/// <see cref="ConnectionProbeStatus.Offline"/> con el motivo y el bucle sigue vivo.
/// </para>
/// <para>
/// Si la base no responde todavía (por ejemplo en el primer arranque, antes de sembrar las claves), el
/// intervalo cae al único valor que la especificación confirma para esta clave, 2 minutos (§2.9.1). No
/// es un valor inventado: es el que la siembra deja guardado. El ciclo siguiente vuelve a intentar leer
/// el valor real de SQLite.
/// </para>
/// </remarks>
public sealed partial class ConnectionMonitor : IConnectionMonitor, IHostedService, IDisposable
{
    /// <summary>Cantidad máxima de entradas del mini-log de cada destino.</summary>
    public const int MaxLogEntries = 30;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly List<TargetState> _states = new();
    private readonly IAppConfigurationStore _configurationStore;
    private readonly ILogger<ConnectionMonitor> _logger;
    private readonly IConnectionMonitorDelay _delay;
    private readonly object _sync = new();

    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private TimeSpan? _interval;
    private int? _slowThresholdMilliseconds;
    private DateTimeOffset? _lastCycleAtUtc;
    private bool _disposed;

    public ConnectionMonitor(
        IEnumerable<IConnectionProbe> probes,
        IAppConfigurationStore configurationStore,
        IConnectionMonitorDelay delay,
        ILogger<ConnectionMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(logger);

        _configurationStore = configurationStore;
        _delay = delay;
        _logger = logger;

        foreach (IConnectionProbe probe in probes)
        {
            ArgumentNullException.ThrowIfNull(probe);
            _states.Add(new TargetState(probe));
        }
    }

    /// <summary>
    /// Intervalo de respaldo cuando la configuración todavía no se puede leer: el único valor confirmado
    /// por la especificación para <c>Connections.AutoCheckMinutes</c> (§2.9.1).
    /// </summary>
    public static TimeSpan ConfirmedAutoCheckInterval { get; } = ResolveConfirmedInterval();

    /// <inheritdoc />
    public IReadOnlyList<ConnectionTargetSnapshot> Snapshots
    {
        get
        {
            lock (_sync)
            {
                return _states
                    .Select(state => new ConnectionTargetSnapshot(
                        state.Probe.TargetKey,
                        state.Probe.TargetName,
                        state.LastResult,
                        state.IsChecking,
                        state.Entries.ToArray()))
                    .ToArray();
            }
        }
    }

    /// <inheritdoc />
    public TimeSpan? Interval
    {
        get
        {
            lock (_sync)
            {
                return _interval;
            }
        }
    }

    /// <inheritdoc />
    public int? SlowThresholdMilliseconds
    {
        get
        {
            lock (_sync)
            {
                return _slowThresholdMilliseconds;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastCycleAtUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastCycleAtUtc;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? Updated;

    /// <summary>Arranca el bucle en segundo plano y devuelve el control sin esperar al primer ciclo.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;

        _logger.LogInformation(
            "Rechequeo automático de conexiones iniciado: {Comprobaciones} destinos en segundo plano",
            _states.Count);

        _loop = Task.Run(() => RunAsync(token), CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <summary>Detiene el bucle con una espera acotada y sin dejar excepciones hacia el host.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? loop = _loop;
        CancellationTokenSource? cancellation = _cancellation;

        if (loop is null || cancellation is null)
        {
            return;
        }

        _logger.LogInformation("Rechequeo automático de conexiones detenido");

        cancellation.Cancel();

        try
        {
            await loop.WaitAsync(StopTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("El rechequeo de conexiones no terminó a tiempo; se deja de esperar");
        }
        catch (OperationCanceledException)
        {
            // Detener el host no es un error del rechequeo.
        }
        finally
        {
            _cancellation = null;
            _loop = null;
            cancellation.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task CheckNowAsync(string targetKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);

        TargetState? state = FindState(targetKey);
        if (state is null)
        {
            _logger.LogWarning("Se pidió comprobar un destino desconocido: {Destino}", targetKey);

            return;
        }

        int? threshold = await ReadSlowThresholdAsync(cancellationToken).ConfigureAwait(false);
        PublishThreshold(threshold);

        await RunProbeAsync(state, threshold, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TimeSpan interval = await ResolveIntervalAsync(cancellationToken).ConfigureAwait(false);
                PublishInterval(interval);

                await RunCycleAsync(cancellationToken).ConfigureAwait(false);
                await _delay.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Fallo inesperado en el rechequeo automático de conexiones");

                // Nada de excepciones no controladas dentro del bucle: se espera y se vuelve a intentar.
                try
                {
                    await _delay.DelayAsync(ConfirmedAutoCheckInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        int? threshold = await ReadSlowThresholdAsync(cancellationToken).ConfigureAwait(false);
        PublishThreshold(threshold);

        foreach (TargetState state in _states.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            await RunProbeAsync(state, threshold, cancellationToken).ConfigureAwait(false);
        }

        lock (_sync)
        {
            _lastCycleAtUtc = DateTimeOffset.UtcNow;
        }

        NotifyUpdated();
    }

    private TargetState? FindState(string targetKey)
    {
        lock (_sync)
        {
            return _states.FirstOrDefault(
                state => string.Equals(state.Probe.TargetKey, targetKey, StringComparison.Ordinal));
        }
    }

    private static TimeSpan ResolveConfirmedInterval()
    {
        string? confirmed = AppConfigurationKeys.ConfirmedInitialValue(AppConfigurationKeys.AutoCheckMinutes);

        return int.TryParse(confirmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
            && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : TimeSpan.FromMinutes(2);
    }

    /// <summary>Estado interno de un destino: su último resultado y su mini-log acotado.</summary>
    private sealed class TargetState
    {
        public TargetState(IConnectionProbe probe)
        {
            Probe = probe;
        }

        public IConnectionProbe Probe { get; }

        public ConnectionProbeResult? LastResult { get; set; }

        public bool IsChecking { get; set; }

        public List<ConnectionProbeResult> Entries { get; } = new();
    }
}
