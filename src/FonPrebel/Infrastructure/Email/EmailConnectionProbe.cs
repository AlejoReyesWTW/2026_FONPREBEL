using System.Diagnostics;
using FonPrebel.Application.Interfaces;

namespace FonPrebel.Infrastructure.Email;

/// <summary>
/// §2.14: comprobación del servidor de correo. Lee la fila única de <c>EmailConfiguration</c> y, si falta
/// el servidor o el puerto, informa <see cref="ConnectionProbeStatus.Unknown"/> con el motivo "sin
/// configurar".
/// </summary>
/// <remarks>
/// <para>
/// El protocolo real del banco sigue pendiente (§2.10, §5.2), así que aquí no hay SMTP ni autenticación
/// y —tampoco— una afirmación de que el correo funcione: con servidor y puerto configurados la sonda
/// delega en <see cref="IEmailConnectionCheck"/>, que todavía no tiene implementación, y por eso informa
/// <see cref="ConnectionProbeStatus.Unknown"/> con el motivo "protocolo de correo pendiente". Un ping no
/// demuestra ni la autenticación ni el envío, y marcarlo verde sería mentirle al analista.
/// </para>
/// <para>
/// Cuando TI defina el mecanismo autorizado se implementa <see cref="IEmailConnectionCheck"/> y se le
/// pasa a esta sonda: es el único cambio de código necesario. Ese día también se usará el proxy de
/// Configuración (§2.9.2, §2.14); hoy esta sonda no abre ninguna conexión, así que no hay nada que
/// enrutar y no se puede afirmar que el correo funcione.
/// </para>
/// </remarks>
public sealed class EmailConnectionProbe : IConnectionProbe
{
    /// <summary>Clave estable del destino.</summary>
    public const string Key = "email";

    /// <summary>Nombre visible del destino.</summary>
    public const string DisplayName = "Correo";

    /// <summary>Motivo cuando no hay servidor o puerto configurados.</summary>
    public const string NotConfiguredReason = "sin configurar";

    /// <summary>Motivo cuando hay configuración pero el mecanismo de correo está pendiente.</summary>
    public const string PendingProtocolReason = "protocolo de correo pendiente";

    /// <summary>Motivo cuando la configuración de correo no se pudo leer.</summary>
    public const string ConfigurationUnavailableReason = "no se pudo leer la configuración de correo";

    private readonly IEmailSettingsStore _settingsStore;
    private readonly IEmailConnectionCheck? _connectionCheck;

    /// <param name="settingsStore">Lectura de la fila única de correo.</param>
    /// <param name="connectionCheck">
    /// Comprobación real del mecanismo de correo; nula mientras el protocolo esté pendiente.
    /// </param>
    public EmailConnectionProbe(IEmailSettingsStore settingsStore, IEmailConnectionCheck? connectionCheck = null)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);

        _settingsStore = settingsStore;
        _connectionCheck = connectionCheck;
    }

    /// <inheritdoc />
    public string TargetKey => Key;

    /// <inheritdoc />
    public string TargetName => DisplayName;

    /// <inheritdoc />
    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset attemptedAt = DateTimeOffset.UtcNow;

        ConfigurationReadResult<EmailSettings> read = await _settingsStore
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!read.Succeeded || read.Value is null)
        {
            return ConnectionProbeResult.Unchecked(
                TargetKey,
                TargetName,
                attemptedAt,
                ConfigurationUnavailableReason,
                $"Sin comprobar: no se pudo leer la configuración de correo ({read.Outcome}).");
        }

        EmailSettings settings = read.Value;

        if (string.IsNullOrWhiteSpace(settings.Host) || settings.Port is not int port || port <= 0)
        {
            return ConnectionProbeResult.Unchecked(
                TargetKey,
                TargetName,
                attemptedAt,
                NotConfiguredReason,
                "Sin comprobar: sin configurar. Faltan el servidor o el puerto del correo en Configuración.");
        }

        if (_connectionCheck is null)
        {
            return ConnectionProbeResult.Unchecked(
                TargetKey,
                TargetName,
                attemptedAt,
                PendingProtocolReason,
                "Sin comprobar: el mecanismo de correo autorizado por el banco está pendiente (§2.10, §5.2), " +
                "así que no se puede afirmar que el correo funcione.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        ConnectionProbeResult result = await _connectionCheck
            .CheckAsync(settings, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        // El resultado de la comprobación se reestampa con este destino: el mini-log y el punto de estado
        // son del correo, no de la implementación concreta del mecanismo.
        return result with
        {
            TargetKey = TargetKey,
            TargetName = TargetName,
            AttemptedAtUtc = attemptedAt,
            DurationMilliseconds = result.DurationMilliseconds ?? (long)Math.Round(
                stopwatch.Elapsed.TotalMilliseconds,
                MidpointRounding.AwayFromZero),
        };
    }
}
