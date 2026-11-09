using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Sección Rutas del servidor de archivos (§1.2 del diseño de configuración). Las cuatro rutas son
/// obligatorias; la validación comprueba que existan y, donde la aplicación escribe, que se pueda
/// escribir. Los valores reales los entrega TI: la sección arranca vacía.
/// </summary>
public sealed class FileRoutesSettingsViewModel : SettingsSectionViewModel
{
    private readonly IAppConfigurationStore _configurationStore;

    public FileRoutesSettingsViewModel(IAppConfigurationStore configurationStore)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);

        _configurationStore = configurationStore;

        Route1 = AddField(
            FileRouteValidation.Route1Parameter,
            FileRouteValidation.ValidateConsolidatedFile,
            hint: "Archivo del consolidado anual.");

        Route2 = AddField(
            FileRouteValidation.Route2Parameter,
            FileRouteValidation.ValidatePolicyFolder,
            hint: "Carpeta con los 13 archivos de póliza.");

        Route3 = AddField(
            FileRouteValidation.Route3Parameter,
            FileRouteValidation.ValidateAnalystFile,
            hint: "Archivo que la fase 2 lee y sobrescribe.");

        Route4 = AddField(
            FileRouteValidation.Route4Parameter,
            FileRouteValidation.ValidatePdfFolder,
            hint: "Carpeta donde se generan los PDFs.");
    }

    /// <summary>Ruta 1: consolidado anual.</summary>
    public ValidatedFieldViewModel Route1 { get; }

    /// <summary>Ruta 2: los 13 archivos de póliza.</summary>
    public ValidatedFieldViewModel Route2 { get; }

    /// <summary>Ruta 3: archivo del analista.</summary>
    public ValidatedFieldViewModel Route3 { get; }

    /// <summary>Ruta 4: carpeta de PDFs.</summary>
    public ValidatedFieldViewModel Route4 { get; }

    public override async Task<ConfigurationStoreOutcome> LoadAsync(CancellationToken cancellationToken = default)
    {
        string[] keys = { AppConfigurationKeys.Route1, AppConfigurationKeys.Route2, AppConfigurationKeys.Route3, AppConfigurationKeys.Route4 };
        ValidatedFieldViewModel[] fields = { Route1, Route2, Route3, Route4 };

        for (int index = 0; index < keys.Length; index++)
        {
            ConfigurationReadResult<string> value = await _configurationStore
                .ReadTextAsync(keys[index], cancellationToken)
                .ConfigureAwait(true);

            if (!value.Succeeded)
            {
                return value.Outcome;
            }

            fields[index].Value = value.Value ?? string.Empty;
        }

        return ConfigurationStoreOutcome.Succeeded;
    }

    public override async Task<ConfigurationWriteResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        string[] keys = { AppConfigurationKeys.Route1, AppConfigurationKeys.Route2, AppConfigurationKeys.Route3, AppConfigurationKeys.Route4 };
        ValidatedFieldViewModel[] fields = { Route1, Route2, Route3, Route4 };

        for (int index = 0; index < keys.Length; index++)
        {
            ConfigurationWriteResult result = await _configurationStore
                .WriteTextAsync(keys[index], fields[index].Value, cancellationToken)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                return result;
            }
        }

        return ConfigurationWriteResult.Success;
    }
}
