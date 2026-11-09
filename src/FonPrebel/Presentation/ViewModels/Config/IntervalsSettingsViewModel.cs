using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Sección Intervalos y concurrencia (§1.3 del diseño de configuración). El intervalo de verificación
/// trae el único valor que el spec confirma (2 minutos); el umbral y los trabajadores están pendientes
/// y por eso se muestran vacíos, sin inventar un número.
/// </summary>
public sealed class IntervalsSettingsViewModel : SettingsSectionViewModel
{
    private readonly IAppConfigurationStore _configurationStore;

    public IntervalsSettingsViewModel(IAppConfigurationStore configurationStore, TimeSpan? minimumIndicatorDuration = null)
        : base(minimumIndicatorDuration)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);

        _configurationStore = configurationStore;

        AutoCheckMinutes = AddField(
            ConcurrencySettingsValidation.AutoCheckParameter,
            ConcurrencySettingsValidation.ValidateAutoCheckMinutes,
            hint: "En minutos; el spec confirma 2.");

        SlowThresholdMs = AddField(
            ConcurrencySettingsValidation.SlowThresholdParameter,
            ConcurrencySettingsValidation.ValidateSlowThresholdMs,
            hint: "En milisegundos; el valor está pendiente.");

        PdfWorkers = AddField(
            ConcurrencySettingsValidation.PdfWorkersParameter,
            ConcurrencySettingsValidation.ValidatePdfWorkers,
            hint: "El paralelismo está pendiente.");

        EmailWorkers = AddField(
            ConcurrencySettingsValidation.EmailWorkersParameter,
            ConcurrencySettingsValidation.ValidateEmailWorkers,
            hint: "El envío es secuencial, uno por uno.");
    }

    /// <summary>Intervalo de verificación automática, en minutos.</summary>
    public ValidatedFieldViewModel AutoCheckMinutes { get; }

    /// <summary>Umbral de conexión lenta, en milisegundos.</summary>
    public ValidatedFieldViewModel SlowThresholdMs { get; }

    /// <summary>Trabajadores para PDFs.</summary>
    public ValidatedFieldViewModel PdfWorkers { get; }

    /// <summary>Trabajadores para correos.</summary>
    public ValidatedFieldViewModel EmailWorkers { get; }

    public override async Task<ConfigurationStoreOutcome> LoadAsync(CancellationToken cancellationToken = default)
    {
        (string Key, ValidatedFieldViewModel Field)[] values =
        {
            (AppConfigurationKeys.AutoCheckMinutes, AutoCheckMinutes),
            (AppConfigurationKeys.SlowThresholdMs, SlowThresholdMs),
            (AppConfigurationKeys.PdfWorkers, PdfWorkers),
            (AppConfigurationKeys.EmailWorkers, EmailWorkers),
        };

        foreach ((string key, ValidatedFieldViewModel field) in values)
        {
            ConfigurationIntResult result = await _configurationStore
                .ReadIntAsync(key, cancellationToken)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                return result.Outcome;
            }

            field.Value = ToText(result);
        }

        return ConfigurationStoreOutcome.Succeeded;
    }

    public override async Task<ConfigurationWriteResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        (string Key, ValidatedFieldViewModel Field)[] values =
        {
            (AppConfigurationKeys.AutoCheckMinutes, AutoCheckMinutes),
            (AppConfigurationKeys.SlowThresholdMs, SlowThresholdMs),
            (AppConfigurationKeys.PdfWorkers, PdfWorkers),
            (AppConfigurationKeys.EmailWorkers, EmailWorkers),
        };

        foreach ((string key, ValidatedFieldViewModel field) in values)
        {
            // §2.9.2 r16: el parámetro pendiente se guarda nulo y no impide guardar el resto.
            if (!TryReadPendingNumber(field, out int? number))
            {
                return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.InvalidValue);
            }

            ConfigurationWriteResult result = await _configurationStore
                .WriteIntAsync(key, number, cancellationToken)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                return result;
            }
        }

        return ConfigurationWriteResult.Success;
    }
}
