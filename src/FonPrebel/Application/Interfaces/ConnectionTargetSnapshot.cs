namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Foto de un destino de conexión para la pantalla: el último resultado, si hay una comprobación en
/// curso y el mini-log acotado de intentos, del más viejo al más nuevo.
/// </summary>
/// <param name="TargetKey">Clave estable del destino.</param>
/// <param name="TargetName">Nombre visible del destino.</param>
/// <param name="LastResult">Último resultado conocido; nulo cuando nunca se comprobó.</param>
/// <param name="IsChecking">Verdadero mientras corre una comprobación de este destino.</param>
/// <param name="Entries">Mini-log acotado, en orden cronológico.</param>
public sealed record ConnectionTargetSnapshot(
    string TargetKey,
    string TargetName,
    ConnectionProbeResult? LastResult,
    bool IsChecking,
    IReadOnlyList<ConnectionProbeResult> Entries)
{
    /// <summary>
    /// Estado que la pantalla muestra: "comprobando" mientras corre, el último estado conocido o
    /// <see cref="ConnectionProbeStatus.Unknown"/> cuando todavía no se comprobó nada.
    /// </summary>
    public ConnectionProbeStatus Status => IsChecking
        ? ConnectionProbeStatus.Checking
        : LastResult?.Status ?? ConnectionProbeStatus.Unknown;
}
