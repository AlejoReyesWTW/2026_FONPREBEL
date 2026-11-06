namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Rechequeo automático de las dos conexiones (§2.14): corre en segundo plano al iniciar y luego cada
/// intervalo configurado, guarda el último resultado de cada destino con su mini-log acotado y permite
/// lanzar una comprobación manual desde la pantalla sin congelar la interfaz.
/// </summary>
/// <remarks>
/// El intervalo y el umbral de lentitud se leen de la configuración en cada ciclo: un cambio en
/// Configuración se aplica sin recompilar y sin reiniciar. Ningún fallo de una sonda llega como
/// excepción a la interfaz; se traduce a un resultado rojo con su motivo.
/// </remarks>
public interface IConnectionMonitor
{
    /// <summary>Foto actual de cada destino, en el orden en que se comprueban.</summary>
    IReadOnlyList<ConnectionTargetSnapshot> Snapshots { get; }

    /// <summary>Intervalo vigente del rechequeo; nulo mientras no se haya leído la configuración.</summary>
    TimeSpan? Interval { get; }

    /// <summary>Umbral de lentitud vigente en milisegundos; nulo cuando está pendiente de definir.</summary>
    int? SlowThresholdMilliseconds { get; }

    /// <summary>Momento en que terminó la última revisión automática, en UTC.</summary>
    DateTimeOffset? LastCycleAtUtc { get; }

    /// <summary>Se dispara cuando cambia cualquier estado visible; puede llegar desde otro hilo.</summary>
    event EventHandler? Updated;

    /// <summary>Comprueba un destino concreto, como lo hace el botón "Comprobar conexión".</summary>
    Task CheckNowAsync(string targetKey, CancellationToken cancellationToken = default);
}
