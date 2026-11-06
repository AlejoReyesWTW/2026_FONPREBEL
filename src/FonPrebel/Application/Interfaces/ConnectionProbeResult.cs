namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Resultado de una comprobación de conexión (§2.14): el estado, la duración medida en milisegundos, el
/// momento del intento, el mensaje que va al mini-log y, cuando corresponde, el motivo.
/// </summary>
/// <remarks>
/// El motivo (<see cref="Reason"/>) cumple dos papeles que la pantalla muestra igual: por qué no se pudo
/// comprobar (<see cref="ConnectionProbeStatus.Unknown"/>, por ejemplo "sin configurar") y por qué falló
/// (<see cref="ConnectionProbeStatus.Offline"/>). Una comprobación que no se hizo nunca se informa como
/// verde ni como roja: eso sería afirmar algo que no se verificó.
/// </remarks>
/// <param name="TargetKey">Clave estable del destino, para agrupar su mini-log.</param>
/// <param name="TargetName">Nombre visible del destino.</param>
/// <param name="Status">Estado resultante de la comprobación.</param>
/// <param name="DurationMilliseconds">Duración medida; nula cuando la comprobación no se hizo.</param>
/// <param name="AttemptedAtUtc">Momento del intento, en UTC.</param>
/// <param name="Message">Mensaje para el mini-log y para la tarjeta.</param>
/// <param name="Reason">Motivo del fallo o de la falta de comprobación; nulo cuando respondió.</param>
public sealed record ConnectionProbeResult(
    string TargetKey,
    string TargetName,
    ConnectionProbeStatus Status,
    long? DurationMilliseconds,
    DateTimeOffset AttemptedAtUtc,
    string Message,
    string? Reason = null)
{
    /// <summary>Indica si el destino respondió: verde o amarillo, siempre tras una comprobación real.</summary>
    public bool Responded =>
        Status is ConnectionProbeStatus.Online or ConnectionProbeStatus.Slow;

    /// <summary>Resultado de una comprobación que no se pudo hacer, con su motivo.</summary>
    public static ConnectionProbeResult Unchecked(
        string targetKey,
        string targetName,
        DateTimeOffset attemptedAtUtc,
        string reason,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ConnectionProbeResult(
            targetKey,
            targetName,
            ConnectionProbeStatus.Unknown,
            DurationMilliseconds: null,
            attemptedAtUtc,
            message,
            reason);
    }

    /// <summary>Resultado de una comprobación en curso, sólo para la pantalla.</summary>
    public static ConnectionProbeResult Checking(string targetKey, string targetName, DateTimeOffset attemptedAtUtc) =>
        new(targetKey, targetName, ConnectionProbeStatus.Checking, null, attemptedAtUtc, "Comprobando…");

    /// <summary>Resultado de una comprobación que respondió.</summary>
    public static ConnectionProbeResult Online(
        string targetKey,
        string targetName,
        DateTimeOffset attemptedAtUtc,
        long durationMilliseconds,
        string message)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);

        return new ConnectionProbeResult(
            targetKey,
            targetName,
            ConnectionProbeStatus.Online,
            durationMilliseconds,
            attemptedAtUtc,
            message);
    }

    /// <summary>Resultado de una comprobación que no respondió, con el motivo concreto.</summary>
    public static ConnectionProbeResult Offline(
        string targetKey,
        string targetName,
        DateTimeOffset attemptedAtUtc,
        long? durationMilliseconds,
        string reason,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (durationMilliseconds is long value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
        }

        return new ConnectionProbeResult(
            targetKey,
            targetName,
            ConnectionProbeStatus.Offline,
            durationMilliseconds,
            attemptedAtUtc,
            message,
            reason);
    }

    /// <summary>
    /// Resultado rojo a partir de una excepción no controlada: la sonda se rompió, así que tampoco hay
    /// confirmación de que el destino responda y se informa el motivo en lenguaje del analista.
    /// </summary>
    public static ConnectionProbeResult Failed(
        string targetKey,
        string targetName,
        DateTimeOffset attemptedAtUtc,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string reason = $"error al comprobar: {exception.Message}";

        return Offline(
            targetKey,
            targetName,
            attemptedAtUtc,
            durationMilliseconds: null,
            reason,
            $"No responde: {reason}.");
    }
}
