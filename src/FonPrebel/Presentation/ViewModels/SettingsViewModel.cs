using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Presentation.ViewModels.Config;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Configuración (V4): compone las secciones colapsables de correo, proxy, rutas, intervalos y
/// credenciales, valida cada campo (Anexo E), guarda en SQLite (S6) y publica si la configuración
/// obligatoria está completa para habilitar INICIAR (S18).
/// </summary>
/// <remarks>
/// No hay valores por defecto inventados: los parámetros sin confirmar se muestran vacíos y sólo se
/// siembran las dos claves que el spec confirma. La pantalla nunca deja caer la aplicación por una base
/// inaccesible: muestra el aviso en un <c>InfoBar</c> de error.
/// </remarks>
public sealed partial class SettingsViewModel : SectionViewModel
{
    /// <summary>Confirmación al guardar la configuración.</summary>
    public const string SavedMessage = "Configuración guardada.";

    /// <summary>Aviso cuando la base local no responde.</summary>
    public const string StorageErrorMessage =
        "No se pudo acceder a la configuración porque la base de datos local no está disponible.";

    /// <summary>
    /// Encabezado del rechazo de §2.9.2 r17: nunca es un mensaje genérico, porque debajo se nombra cada
    /// campo que impide guardar con el motivo de su comprobación.
    /// </summary>
    public const string SaveBlockedHeader = "No se guardó la configuración.";

    private readonly ConfigurationReadinessGate _gate;
    private readonly IConnectionMonitor _connectionMonitor;
    private readonly IConfirmationDialog _confirmationDialog;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusError;

    [ObservableProperty]
    private bool _hasStorageError;

    public SettingsViewModel(
        IAppConfigurationStore configurationStore,
        IEmailSettingsStore emailStore,
        IEmailCredentialService credentialService,
        ConfigurationReadinessGate gate,
        IConnectionMonitor connectionMonitor,
        IConfirmationDialog confirmationDialog,
        ILogger<SettingsViewModel> logger,
        TimeSpan? minimumIndicatorDuration = null)
        : base("settings", "Configuración",
               "Correo, proxy, rutas del servidor de archivos, intervalos y credenciales. Mientras falte un campo obligatorio, el proceso no arranca.")
    {
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(emailStore);
        ArgumentNullException.ThrowIfNull(credentialService);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(connectionMonitor);
        ArgumentNullException.ThrowIfNull(confirmationDialog);
        ArgumentNullException.ThrowIfNull(logger);

        _gate = gate;
        _connectionMonitor = connectionMonitor;
        _confirmationDialog = confirmationDialog;
        _logger = logger;

        Email = new EmailSettingsViewModel(configurationStore, emailStore, minimumIndicatorDuration);
        Proxy = new ProxySettingsViewModel(configurationStore, minimumIndicatorDuration);
        Routes = new FileRoutesSettingsViewModel(configurationStore, minimumIndicatorDuration);
        Intervals = new IntervalsSettingsViewModel(configurationStore, minimumIndicatorDuration);
        Credentials = new CredentialSettingsViewModel(credentialService);

        foreach (SettingsSectionViewModel section in Sections)
        {
            section.FieldStateChanged += OnFieldStateChanged;
        }

        Credentials.StateChanged += OnFieldStateChanged;
        Credentials.PropertyChanged += OnCredentialPropertyChanged;

        // §2.9.2 r10 y r14: el seguimiento de cambios mira el valor de cada campo y el interruptor del proxy.
        foreach (ValidatedFieldViewModel field in AllFields)
        {
            field.PropertyChanged += OnFieldValueChanged;
        }

        Proxy.PropertyChanged += OnProxyPropertyChanged;

        PublishReadiness();
    }

    /// <summary>Sección de correo.</summary>
    public EmailSettingsViewModel Email { get; }

    /// <summary>Sección de proxy.</summary>
    public ProxySettingsViewModel Proxy { get; }

    /// <summary>Sección de rutas del servidor de archivos.</summary>
    public FileRoutesSettingsViewModel Routes { get; }

    /// <summary>Sección de intervalos y concurrencia.</summary>
    public IntervalsSettingsViewModel Intervals { get; }

    /// <summary>Sección de credenciales.</summary>
    public CredentialSettingsViewModel Credentials { get; }

    /// <summary>Texto del aviso de base no disponible.</summary>
    public string StorageErrorText => StorageErrorMessage;

    /// <summary>Las cuatro secciones con campos de texto.</summary>
    public IReadOnlyList<SettingsSectionViewModel> Sections => new SettingsSectionViewModel[]
    {
        Email,
        Proxy,
        Routes,
        Intervals,
    };

    /// <summary>Todos los campos de la pantalla, incluido el texto del proxy.</summary>
    public IReadOnlyList<ValidatedFieldViewModel> AllFields =>
        Email.Fields
            .Concat(Proxy.Fields)
            .Concat(Routes.Fields)
            .Concat(Intervals.Fields)
            .ToArray();

    /// <summary>Carga los valores guardados y vuelve a validar todos los campos.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            ConfigurationStoreOutcome outcome = ConfigurationStoreOutcome.Succeeded;

            foreach (SettingsSectionViewModel section in Sections)
            {
                ConfigurationStoreOutcome sectionOutcome = await section
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(true);

                if (sectionOutcome != ConfigurationStoreOutcome.Succeeded)
                {
                    outcome = sectionOutcome;
                }
            }

            SetStorageState(outcome);

            await Credentials.RefreshAsync(cancellationToken).ConfigureAwait(true);
            await RevalidateAllAsync().ConfigureAwait(true);

            // §2.9.2 r10: lo recién leído es la referencia contra la que se mide un cambio real.
            AcceptStoredValues();
        }
        finally
        {
            IsBusy = false;
            PublishReadiness();
        }
    }

    /// <summary>
    /// Guarda la configuración con un cambio real. Sin diferencia con lo almacenado no escribe nada y no
    /// muestra ningún diálogo (§2.9.2 r11). Un valor escrito que no pasa su comprobación rechaza el guardado
    /// nombrando los campos (§2.9.2 r17); un obligatorio vacío no bloquea, deja la configuración incompleta y
    /// dispara el aviso modal previo a escribir (§2.9.2 r12 y r16). Devuelve verdadero si se escribió.
    /// </summary>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            // §2.9.2 r11: con los valores idénticos a los almacenados no se escribe nada ni se muestra diálogo.
            if (!HasChanges)
            {
                return false;
            }

            // Una comprobación en curso se espera; nunca se rechaza el guardado por estar todavía validando.
            await RevalidateAllAsync().ConfigureAwait(true);

            ValidatedFieldViewModel[] blockers = AllFields.Where(field => field.IsBlocking).ToArray();
            if (blockers.Length > 0)
            {
                SetStatus(DescribeBlockers(blockers), isError: true);
                PublishReadiness();

                return false;
            }

            // §2.9.2 r12: un guardado válido que deja la configuración incompleta para operar se persiste,
            // pero antes el analista confirma; si cancela no se escribe nada y los valores quedan en pantalla.
            if (!IsConfigurationCompleteForOperation && !ConfirmIncompleteSave())
            {
                SetStatus(SaveCancelledMessage, isError: false);
                PublishReadiness();

                return false;
            }

            ConfigurationWriteResult result = await SaveAllAsync(cancellationToken).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                SetStatus(StorageErrorMessage, isError: true);
                HasStorageError = true;
                PublishReadiness();

                return false;
            }

            HasStorageError = false;
            SetStatus(SavedMessage, isError: false);

            // Anexo E: al guardar se limpian los indicadores temporales; el valor sigue siendo válido.
            foreach (SettingsSectionViewModel section in Sections)
            {
                section.ClearIndicators();
            }

            // §2.9.2 r13: lo guardado sobrescribe la clave y pasa a ser la referencia de comparación.
            AcceptStoredValues();

            PublishReadiness();

            // §2.9.2 r2: con los datos ya guardados se ofrece comprobar las conexiones. Se ofrece, no se
            // ejecuta sola: la comprobación toca la red y tarda, así que la decide el analista.
            HasConnectionOffer = true;
            HasConnectionOfferResult = false;
            ConnectionOfferResultText = string.Empty;

            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Carga la configuración desde la pantalla.</summary>
    [RelayCommand]
    private Task Load() => LoadAsync();

    /// <summary>Guarda la configuración desde la pantalla; sólo se puede pulsar con un cambio real.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task Save() => SaveAsync();

    private async Task<ConfigurationWriteResult> SaveAllAsync(CancellationToken cancellationToken)
    {
        ConfigurationWriteResult result = await Email.SaveAsync(cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            return result;
        }

        result = await Proxy.SaveAsync(cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            return result;
        }

        result = await Routes.SaveAsync(cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            return result;
        }

        return await Intervals.SaveAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task RevalidateAllAsync() =>
        await Task.WhenAll(Sections.Select(section => section.RevalidateAllAsync())).ConfigureAwait(true);

    /// <summary>
    /// Publica si la configuración obligatoria está completa; el gate sólo se abre con todo obligatorio
    /// completado y válido, así un campo vacío no habilita INICIAR (§2.9.2 r16, Anexo D).
    /// </summary>
    private void PublishReadiness()
    {
        _gate.Report(IsConfigurationCompleteForOperation && !IsBusy);
    }

    private void SetStorageState(ConfigurationStoreOutcome outcome)
    {
        HasStorageError = outcome != ConfigurationStoreOutcome.Succeeded;

        if (!HasStorageError)
        {
            return;
        }

        _logger.LogWarning("No se pudo leer la configuración: {Outcome}", outcome);
        SetStatus(StorageErrorMessage, isError: true);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        HasStatusError = isError;
    }

    private void OnFieldStateChanged(object? sender, EventArgs e) => PublishReadiness();

    private void OnCredentialPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CredentialSettingsViewModel.HasStorageError))
        {
            PublishReadiness();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        PublishReadiness();
        NotifySaveStateChanged();
    }
}
