using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Sección Proxy (§2.9.2 r7-r9). El interruptor decide si el texto se usa: apagado, el campo queda
/// deshabilitado y vacío; encendido, pasa a ser obligatorio. No hay usuario ni contraseña de proxy.
/// </summary>
public sealed partial class ProxySettingsViewModel : SettingsSectionViewModel
{
    private readonly IAppConfigurationStore _configurationStore;

    [ObservableProperty]
    private bool _isProxyEnabled;

    public ProxySettingsViewModel(IAppConfigurationStore configurationStore)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);

        _configurationStore = configurationStore;

        ProxyText = AddField(
            ProxySettingsValidation.ProxyTextParameter,
            value => ProxySettingsValidation.ValidateText(IsProxyEnabled, value),
            isEnabled: false,
            hint: "Sólo se usa cuando el proxy está activado.");
    }

    /// <summary>Texto del proxy.</summary>
    public ValidatedFieldViewModel ProxyText { get; }

    public override async Task<ConfigurationStoreOutcome> LoadAsync(CancellationToken cancellationToken = default)
    {
        ConfigurationBoolResult enabled = await _configurationStore
            .ReadBoolAsync(AppConfigurationKeys.ProxyEnabled, cancellationToken)
            .ConfigureAwait(true);

        if (!enabled.Succeeded)
        {
            return enabled.Outcome;
        }

        ConfigurationReadResult<string> text = await _configurationStore
            .ReadTextAsync(AppConfigurationKeys.ProxyText, cancellationToken)
            .ConfigureAwait(true);

        if (!text.Succeeded)
        {
            return text.Outcome;
        }

        IsProxyEnabled = enabled.Value ?? false;
        ProxyText.Value = text.Value ?? string.Empty;

        return ConfigurationStoreOutcome.Succeeded;
    }

    public override async Task<ConfigurationWriteResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        ConfigurationWriteResult enabled = await _configurationStore
            .WriteBoolAsync(AppConfigurationKeys.ProxyEnabled, IsProxyEnabled, cancellationToken)
            .ConfigureAwait(true);

        if (!enabled.Succeeded)
        {
            return enabled;
        }

        string? text = IsProxyEnabled ? ProxyText.Value : null;

        return await _configurationStore
            .WriteTextAsync(AppConfigurationKeys.ProxyText, text, cancellationToken)
            .ConfigureAwait(true);
    }

    partial void OnIsProxyEnabledChanged(bool value)
    {
        ProxyText.IsEnabled = value;

        if (!value)
        {
            // Proxy apagado: el texto queda deshabilitado y vacío (§2.9.2 r7).
            ProxyText.Value = string.Empty;
        }
    }
}
