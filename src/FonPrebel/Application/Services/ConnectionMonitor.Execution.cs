using FonPrebel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// Ejecución de un intento de comprobación y los ayudantes que lo rodean: lectura del umbral desde
/// SQLite, traducción de errores a un resultado rojo, registro en el mini-log acotado y aviso a la
/// pantalla. Se separa del bucle para que ningún archivo de código pase el límite de 400 líneas (S2).
/// </summary>
public sealed partial class ConnectionMonitor
{
    private async Task<ConnectionProbeResult> RunProbeAsync(
        TargetState state,
        int? thresholdMilliseconds,
        CancellationToken cancellationToken)
    {
        SetChecking(state, isChecking: true);
        NotifyUpdated();

        _logger.LogInformation("Comprobando conexión con {Destino}", state.Probe.TargetName);

        ConnectionProbeResult result;

        try
        {
            result = await state.Probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetChecking(state, isChecking: false);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "La comprobación de {Destino} falló con un error no controlado",
                state.Probe.TargetName);

            result = ConnectionProbeResult.Failed(
                state.Probe.TargetKey,
                state.Probe.TargetName,
                DateTimeOffset.UtcNow,
                exception);
        }

        result = ConnectionStatePolicy.ApplySlowThreshold(result, thresholdMilliseconds);
        Record(state, result);
        SetChecking(state, isChecking: false);

        _logger.LogInformation(
            "Conexión con {Destino}: {Estado}. {Mensaje}",
            state.Probe.TargetName,
            result.Status,
            result.Message);

        return result;
    }

    /// <summary>Intervalo vigente leído de SQLite; si no se puede leer, el confirmado por la spec.</summary>
    private async Task<TimeSpan> ResolveIntervalAsync(CancellationToken cancellationToken)
    {
        ConfigurationDurationResult read = await _configurationStore
            .ReadDurationAsync(
                AppConfigurationKeys.AutoCheckMinutes,
                ConfigurationTimeUnit.Minutes,
                cancellationToken)
            .ConfigureAwait(false);

        if (read.Succeeded && read.Value is TimeSpan configured && configured > TimeSpan.Zero)
        {
            return configured;
        }

        _logger.LogWarning(
            "No se pudo leer {Clave} ({Outcome}); se usa el intervalo confirmado por la especificación: {Intervalo}",
            AppConfigurationKeys.AutoCheckMinutes,
            read.Outcome,
            ConfirmedAutoCheckInterval);

        return ConfirmedAutoCheckInterval;
    }

    /// <summary>
    /// Umbral de lentitud vigente. Nulo significa "pendiente de definir" (§2.14, §5.6): la pantalla lo
    /// informa como sin umbral y ninguna conexión se marca como lenta.
    /// </summary>
    private async Task<int?> ReadSlowThresholdAsync(CancellationToken cancellationToken)
    {
        ConfigurationIntResult read = await _configurationStore
            .ReadIntAsync(AppConfigurationKeys.SlowThresholdMs, cancellationToken)
            .ConfigureAwait(false);

        if (!read.Succeeded)
        {
            _logger.LogWarning(
                "No se pudo leer {Clave} ({Outcome}); ninguna conexión se marcará como lenta",
                AppConfigurationKeys.SlowThresholdMs,
                read.Outcome);

            return null;
        }

        return ConnectionStatePolicy.NormalizeThreshold(read.Value);
    }

    private void Record(TargetState state, ConnectionProbeResult result)
    {
        lock (_sync)
        {
            state.LastResult = result;
            state.Entries.Add(result);

            while (state.Entries.Count > MaxLogEntries)
            {
                state.Entries.RemoveAt(0);
            }
        }

        NotifyUpdated();
    }

    private void SetChecking(TargetState state, bool isChecking)
    {
        lock (_sync)
        {
            state.IsChecking = isChecking;
        }
    }

    private void PublishInterval(TimeSpan interval)
    {
        bool changed;

        lock (_sync)
        {
            changed = _interval != interval;
            _interval = interval;
        }

        if (changed)
        {
            NotifyUpdated();
        }
    }

    private void PublishThreshold(int? threshold)
    {
        bool changed;

        lock (_sync)
        {
            changed = _slowThresholdMilliseconds != threshold;
            _slowThresholdMilliseconds = threshold;
        }

        if (changed)
        {
            NotifyUpdated();
        }
    }

    private void NotifyUpdated()
    {
        EventHandler? handler = Updated;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            // Un suscriptor roto no puede tumbar el rechequeo ni la aplicación.
            _logger.LogError(exception, "Un suscriptor del rechequeo de conexiones falló");
        }
    }
}
