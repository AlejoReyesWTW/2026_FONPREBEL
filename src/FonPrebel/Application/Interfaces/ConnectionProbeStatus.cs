namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Estado visible de una conexión (§2.14, ficha V6). El punto de estado de la pantalla sólo puede
/// mostrar verde, amarillo o rojo después de una comprobación real.
/// </summary>
/// <remarks>
/// <see cref="Unknown"/> y <see cref="Checking"/> existen para no mentir: mientras no haya una
/// comprobación efectiva, la pantalla no afirma ni que la conexión responde ni que falló.
/// <see cref="Slow"/> sólo se asigna con un umbral configurado
/// (<c>Connections.SlowThresholdMs</c>), que sigue pendiente en la especificación (§2.14, §5.6).
/// </remarks>
public enum ConnectionProbeStatus
{
    /// <summary>No se comprobó nada todavía, o no había configuración para comprobar.</summary>
    Unknown = 0,

    /// <summary>La comprobación está en curso.</summary>
    Checking = 1,

    /// <summary>Comprobado: el destino responde.</summary>
    Online = 2,

    /// <summary>Comprobado: responde, pero por encima del umbral de lentitud configurado.</summary>
    Slow = 3,

    /// <summary>Comprobado: el destino no responde.</summary>
    Offline = 4,
}
