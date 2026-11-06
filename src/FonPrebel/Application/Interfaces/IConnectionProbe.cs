namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Una comprobación de conexión concreta (§2.14): el servidor de archivos o el servidor de correo.
/// </summary>
/// <remarks>
/// La sonda mide y responde <see cref="ConnectionProbeStatus.Online"/>, <see cref="ConnectionProbeStatus.Offline"/>
/// o <see cref="ConnectionProbeStatus.Unknown"/>; nunca asigna <see cref="ConnectionProbeStatus.Slow"/>
/// porque el umbral de lentitud es un parámetro de configuración pendiente (§2.14, §5.6) y lo aplica
/// <c>ConnectionStatePolicy</c> con el valor vigente. Cada llamada lee la configuración guardada, así
/// que un cambio en Configuración se refleja en la siguiente comprobación sin reiniciar.
/// </remarks>
public interface IConnectionProbe
{
    /// <summary>Clave estable del destino; agrupa su mini-log y su punto de estado.</summary>
    string TargetKey { get; }

    /// <summary>Nombre visible del destino.</summary>
    string TargetName { get; }

    /// <summary>Comprueba el destino y devuelve el resultado tipado, sin lanzar por un fallo de red.</summary>
    Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}
