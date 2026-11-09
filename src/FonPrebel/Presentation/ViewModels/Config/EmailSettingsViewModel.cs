using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Sección Correo (V4 y §1.1 del diseño de configuración). Todos los valores están pendientes de
/// confirmar con TI, así que la sección se muestra vacía salvo lo que ya esté guardado.
/// </summary>
public sealed class EmailSettingsViewModel : SettingsSectionViewModel
{
    private readonly IAppConfigurationStore _configurationStore;
    private readonly IEmailSettingsStore _emailStore;

    public EmailSettingsViewModel(
        IAppConfigurationStore configurationStore,
        IEmailSettingsStore emailStore,
        TimeSpan? minimumIndicatorDuration = null)
        : base(minimumIndicatorDuration)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(emailStore);

        _configurationStore = configurationStore;
        _emailStore = emailStore;

        Host = AddField(
            EmailSettingsValidation.HostParameter,
            EmailSettingsValidation.ValidateHost,
            hint: "Servidor de correo que confirme TI.");

        Port = AddField(
            EmailSettingsValidation.PortParameter,
            EmailSettingsValidation.ValidatePort,
            hint: "Obligatorio siempre, con o sin proxy.");

        Security = AddField(
            EmailSettingsValidation.SecurityParameter,
            EmailSettingsValidation.ValidateSecurity,
            hint: "Los valores admitidos están pendientes de confirmar.");

        Sender = AddField(
            EmailSettingsValidation.SenderParameter,
            EmailSettingsValidation.ValidateSender);

        Account = AddField(
            EmailSettingsValidation.AccountParameter,
            value => EmailSettingsValidation.ValidateAccount(value, requiredByProtocol: false),
            hint: "Sólo es obligatoria si el mecanismo de autenticación usa usuario; está pendiente.");

        SendTimeout = AddField(
            EmailSettingsValidation.SendTimeoutParameter,
            EmailSettingsValidation.ValidateSendTimeout,
            hint: "En segundos.");
    }

    /// <summary>Servidor de correo.</summary>
    public ValidatedFieldViewModel Host { get; }

    /// <summary>Puerto del servidor de correo.</summary>
    public ValidatedFieldViewModel Port { get; }

    /// <summary>Seguridad de la conexión.</summary>
    public ValidatedFieldViewModel Security { get; }

    /// <summary>Remitente visible.</summary>
    public ValidatedFieldViewModel Sender { get; }

    /// <summary>Cuenta de correo; condicional según el protocolo.</summary>
    public ValidatedFieldViewModel Account { get; }

    /// <summary>Tiempo máximo de espera por envío, en segundos.</summary>
    public ValidatedFieldViewModel SendTimeout { get; }

    public override async Task<ConfigurationStoreOutcome> LoadAsync(CancellationToken cancellationToken = default)
    {
        ConfigurationReadResult<EmailSettings> settings = await _emailStore
            .ReadAsync(cancellationToken)
            .ConfigureAwait(true);

        if (!settings.Succeeded)
        {
            return settings.Outcome;
        }

        EmailSettings values = settings.Value ?? EmailSettings.Empty;

        Host.Value = values.Host ?? string.Empty;
        Port.Value = values.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        Security.Value = values.Security ?? string.Empty;
        Sender.Value = values.SenderAddress ?? string.Empty;
        Account.Value = values.Account ?? string.Empty;

        ConfigurationIntResult timeout = await _configurationStore
            .ReadIntAsync(AppConfigurationKeys.SendTimeoutSeconds, cancellationToken)
            .ConfigureAwait(true);

        if (!timeout.Succeeded)
        {
            return timeout.Outcome;
        }

        SendTimeout.Value = ToText(timeout);

        return ConfigurationStoreOutcome.Succeeded;
    }

    public override async Task<ConfigurationWriteResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        // §2.9.2 r16: un número vacío es un parámetro pendiente y se guarda nulo; un valor escrito que no
        // es un entero ya quedó bloqueado por la validación del campo y nunca llega hasta acá.
        if (!TryReadPendingNumber(Port, out int? port) || !TryReadPendingNumber(SendTimeout, out int? timeoutSeconds))
        {
            return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.InvalidValue);
        }

        EmailSettings settings = new(
            Host.Value,
            port,
            Security.Value,
            Account.Value,
            Sender.Value);

        ConfigurationWriteResult email = await _emailStore.WriteAsync(settings, cancellationToken).ConfigureAwait(true);
        if (!email.Succeeded)
        {
            return email;
        }

        return await _configurationStore
            .WriteIntAsync(AppConfigurationKeys.SendTimeoutSeconds, timeoutSeconds, cancellationToken)
            .ConfigureAwait(true);
    }
}
