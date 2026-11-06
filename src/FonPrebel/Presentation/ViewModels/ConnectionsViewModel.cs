using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Conexiones (ficha V6, §2.14): dos tarjetas, una por servidor, cada una con su punto de estado,
/// los datos del último intento, el botón "Comprobar conexión" y su mini-log; arriba, el estado del
/// rechequeo automático con el intervalo vigente.
/// </summary>
/// <remarks>
/// La pantalla no comprueba nada por su cuenta: pide el resultado al monitor, que corre en segundo plano
/// y avisa desde otro hilo. El aviso se marshalea al hilo de interfaz, así que la ventana nunca se congela.
/// Mientras no haya comprobación real el punto queda gris y dice "Sin comprobar": nunca verde.
/// </remarks>
public sealed partial class ConnectionsViewModel : SectionViewModel, IDisposable
{
    private readonly IConnectionMonitor _monitor;
    private readonly Dispatcher? _dispatcher;
    private readonly Dictionary<string, ConnectionTargetViewModel> _targetsByKey = new(StringComparer.Ordinal);

    [ObservableProperty]
    private string _autoCheckText = string.Empty;

    [ObservableProperty]
    private string _slowThresholdText = string.Empty;

    [ObservableProperty]
    private string _lastReviewText = string.Empty;

    public ConnectionsViewModel(IConnectionMonitor monitor)
        : base(
            "connections",
            "Conexiones",
            "Dos comprobaciones independientes, servidor de archivos y correo, con su mini-log y su punto de estado. El rechequeo automático corre en segundo plano.")
    {
        ArgumentNullException.ThrowIfNull(monitor);

        _monitor = monitor;
        _dispatcher = System.Windows.Application.Current?.Dispatcher;

        foreach (ConnectionTargetSnapshot snapshot in monitor.Snapshots)
        {
            ConnectionTargetViewModel target = new(snapshot.TargetKey, snapshot.TargetName, CheckAsync);

            _targetsByKey[snapshot.TargetKey] = target;
            Targets.Add(target);
        }

        monitor.Updated += OnMonitorUpdated;
        Refresh();
    }

    /// <summary>Tarjetas de la pantalla, en el orden en que se comprueban los destinos.</summary>
    public ObservableCollection<ConnectionTargetViewModel> Targets { get; } = new();

    /// <inheritdoc />
    public void Dispose() => _monitor.Updated -= OnMonitorUpdated;

    /// <summary>Vuelve a pintar la pantalla desde el último estado del monitor.</summary>
    public void Refresh()
    {
        foreach (ConnectionTargetSnapshot snapshot in _monitor.Snapshots)
        {
            if (_targetsByKey.TryGetValue(snapshot.TargetKey, out ConnectionTargetViewModel? target))
            {
                target.Apply(snapshot);
            }
        }

        AutoCheckText = FormatAutoCheck(_monitor.Interval);
        SlowThresholdText = FormatSlowThreshold(_monitor.SlowThresholdMilliseconds);
        LastReviewText = FormatLastReview(_monitor.LastCycleAtUtc);
    }

    private Task CheckAsync(string targetKey) => _monitor.CheckNowAsync(targetKey);

    private void OnMonitorUpdated(object? sender, EventArgs e)
    {
        Dispatcher? dispatcher = _dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Refresh();

            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Refresh));
    }

    private static string FormatAutoCheck(TimeSpan? interval)
    {
        if (interval is not TimeSpan value)
        {
            return "Rechequeo automático: todavía sin intervalo leído de Configuración.";
        }

        int minutes = (int)Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero);
        string unit = minutes == 1 ? "minuto" : "minutos";

        return $"Rechequeo automático: activo cada {minutes} {unit}, en segundo plano.";
    }

    private static string FormatSlowThreshold(int? slowThresholdMilliseconds) =>
        slowThresholdMilliseconds is int milliseconds
            ? $"Umbral de conexión lenta: {milliseconds} ms."
            : "Umbral de conexión lenta: sin umbral definido en la especificación; el punto amarillo no se usa hasta configurarlo.";

    private static string FormatLastReview(DateTimeOffset? lastCycleAtUtc) =>
        lastCycleAtUtc is DateTimeOffset moment
            ? $"Última revisión automática: {moment.ToLocalTime():dd/MM/yyyy HH:mm:ss}."
            : "Última revisión automática: todavía no se ejecutó.";
}

/// <summary>Textos visibles de los cinco estados de una conexión (§2.14, V6).</summary>
internal static class ConnectionStatusText
{
    /// <summary>Estado sin comprobación real: nunca verde ni rojo.</summary>
    public const string Unknown = "Sin comprobar";

    /// <summary>Comprobación en curso.</summary>
    public const string Checking = "Comprobando…";

    /// <summary>Punto verde: el destino respondió.</summary>
    public const string Online = "Responde";

    /// <summary>Punto amarillo: respondió por encima del umbral configurado.</summary>
    public const string Slow = "Lenta";

    /// <summary>Punto rojo: la comprobación se hizo y falló.</summary>
    public const string Offline = "No responde";

    private static readonly Brush UnknownBrush = CreateBrush(0x8A, 0x88, 0x86);
    private static readonly Brush CheckingBrush = CreateBrush(0x00, 0x78, 0xD4);
    private static readonly Brush OnlineBrush = CreateBrush(0x0F, 0x7B, 0x0F);
    private static readonly Brush SlowBrush = CreateBrush(0x9D, 0x5D, 0x00);
    private static readonly Brush OfflineBrush = CreateBrush(0xC4, 0x2B, 0x1C);

    /// <summary>Texto del estado.</summary>
    public static string For(ConnectionProbeStatus status) => status switch
    {
        ConnectionProbeStatus.Online => Online,
        ConnectionProbeStatus.Slow => Slow,
        ConnectionProbeStatus.Offline => Offline,
        ConnectionProbeStatus.Checking => Checking,
        _ => Unknown,
    };

    /// <summary>Color del punto de estado.</summary>
    public static Brush BrushFor(ConnectionProbeStatus status) => status switch
    {
        ConnectionProbeStatus.Online => OnlineBrush,
        ConnectionProbeStatus.Slow => SlowBrush,
        ConnectionProbeStatus.Offline => OfflineBrush,
        ConnectionProbeStatus.Checking => CheckingBrush,
        _ => UnknownBrush,
    };

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        SolidColorBrush brush = new(Color.FromRgb(red, green, blue));
        brush.Freeze();

        return brush;
    }
}

/// <summary>
/// Una tarjeta de la pantalla Conexiones: punto de estado, último intento con su duración, botón
/// "Comprobar conexión" y mini-log acotado de intentos.
/// </summary>
public sealed partial class ConnectionTargetViewModel : ObservableObject
{
    private readonly Func<string, Task> _check;

    [ObservableProperty]
    private string _statusText = ConnectionStatusText.Unknown;

    [ObservableProperty]
    private Brush _statusBrush = ConnectionStatusText.BrushFor(ConnectionProbeStatus.Unknown);

    [ObservableProperty]
    private string _lastAttemptText = "Todavía no se comprobó desde que abrió la aplicación.";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _reasonText = string.Empty;

    [ObservableProperty]
    private bool _hasReason;

    public ConnectionTargetViewModel(string targetKey, string targetName, Func<string, Task> check)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentNullException.ThrowIfNull(check);

        TargetKey = targetKey;
        TargetName = targetName;
        _check = check;

        CheckCommand = new AsyncRelayCommand(() => _check(TargetKey));
    }

    /// <summary>Clave del destino.</summary>
    public string TargetKey { get; }

    /// <summary>Nombre visible del destino.</summary>
    public string TargetName { get; }

    /// <summary>Líneas del mini-log, del intento más viejo al más nuevo.</summary>
    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>Texto del mini-log cuando todavía no hay intentos.</summary>
    public string EmptyLogText => "Sin intentos todavía.";

    /// <summary>Botón "Comprobar conexión" de la tarjeta.</summary>
    public IAsyncRelayCommand CheckCommand { get; }

    /// <summary>Vuelca el estado del monitor en la tarjeta.</summary>
    public void Apply(ConnectionTargetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ConnectionProbeStatus status = snapshot.Status;

        StatusText = ConnectionStatusText.For(status);
        StatusBrush = ConnectionStatusText.BrushFor(status);
        IsChecking = snapshot.IsChecking;
        LastAttemptText = FormatLastAttempt(snapshot.LastResult);
        ReasonText = snapshot.LastResult?.Reason ?? string.Empty;
        HasReason = !string.IsNullOrWhiteSpace(ReasonText);

        ReplaceLog(snapshot.Entries);
    }

    private void ReplaceLog(IReadOnlyList<ConnectionProbeResult> entries)
    {
        LogLines.Clear();

        foreach (ConnectionProbeResult entry in entries)
        {
            LogLines.Add(
                $"{entry.AttemptedAtUtc.ToLocalTime():HH:mm:ss} · {ConnectionStatusText.For(entry.Status)}: {entry.Message}");
        }
    }

    private static string FormatLastAttempt(ConnectionProbeResult? result)
    {
        if (result is null)
        {
            return "Todavía no se comprobó desde que abrió la aplicación.";
        }

        string moment = result.AttemptedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        string duration = result.DurationMilliseconds is long milliseconds
            ? $"{milliseconds} ms"
            : "sin duración medida";

        return $"Último intento: {moment} · {duration}.";
    }
}
