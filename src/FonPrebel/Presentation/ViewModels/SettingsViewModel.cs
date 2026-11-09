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

    /// <summary>Aviso cuando se intenta guardar con valores inválidos.</summary>
    public const string InvalidMessage =
        "Revisa los campos marcados: hay parámetros obligatorios vacíos o con formato inválido.";

    private readonly ConfigurationReadinessGate _gate;
    private readonly IConnectionMonitor _connectionMonitor;
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
        ILogger<SettingsViewModel> logger)
        : base("settings", "Configuración",
               "Correo, proxy, rutas del servidor de archivos, intervalos y credenciales. Mientras falte un campo obligatorio, el proceso no arranca.")
    {
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(emailStore);
        ArgumentNullException.ThrowIfNull(credentialService);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(connectionMonitor);
        ArgumentNullException.ThrowIfNull(logger);

        _gate = gate;
        _connectionMonitor = connectionMonitor;
        _logger = logger;

        Email = new EmailSettingsViewModel(configurationStore, emailStore);
        Proxy = new ProxySettingsViewModel(configurationStore);
        Routes = new FileRoutesSettingsViewModel(configurationStore);
        Intervals = new IntervalsSettingsViewModel(configurationStore);
        Credentials = new CredentialSettingsViewModel(credentialService);

        foreach (SettingsSectionViewModel section in Sections)
        {
            section.FieldStateChanged += OnFieldStateChanged;
        }

        Credentials.StateChanged += OnFieldStateChanged;
        Credentials.PropertyChanged += OnCredentialPropertyChanged;

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
        }
        finally
        {
            IsBusy = false;
            PublishReadiness();
        }
    }

    /// <summary>
    /// Valida todos los campos y, sólo si no hay valores inválidos, guarda la configuración. Nunca
    /// persiste un valor inválido. Devuelve verdadero cuando la escritura se completó.
    /// </summary>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            await RevalidateAllAsync().ConfigureAwait(true);

            if (AllFields.Any(field => !field.IsValid))
            {
                SetStatus(InvalidMessage, isError: true);
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

    /// <summary>Guarda la configuración desde la pantalla.</summary>
    [RelayCommand]
    private Task Save() => SaveAsync();

    /// <summary>
    /// Texto de la oferta que aparece tras guardar (§2.9.2 r2): comprobar las conexiones con los datos
    /// recién guardados. Se ofrece, no se ejecuta sola, porque una comprobación real toca la red y tarda.
    /// </summary>
    public string ConnectionOfferMessage { get; } =
        "¿Quieres comprobar ahora el servidor de archivos y el correo con los datos guardados?";

    /// <summary>Verdadero mientras la oferta de comprobar las conexiones está a la vista.</summary>
    [ObservableProperty]
    private bool _hasConnectionOffer;

    /// <summary>Resultado de la comprobación ofrecida, una línea por destino; vacío mientras no se ejecutó.</summary>
    [ObservableProperty]
    private string _connectionOfferResultText = string.Empty;

    /// <summary>Verdadero cuando la comprobación ofrecida dejó un resultado para mostrar.</summary>
    [ObservableProperty]
    private bool _hasConnectionOfferResult;

    /// <summary>
    /// Corre la comprobación de los destinos que el monitor ya declara, sin repetir sus claves acá, y deja
    /// el resultado visible con el motivo de los que no se pudieron comprobar.
    /// </summary>
    [RelayCommand]
    private async Task OfferConnectionTest()
    {
        IReadOnlyList<ConnectionTargetSnapshot> targets = _connectionMonitor.Snapshots;

        if (targets.Count == 0)
        {
            HasConnectionOffer = false;

            return;
        }

        HasConnectionOffer = false;
        IsBusy = true;

        try
        {
            foreach (ConnectionTargetSnapshot target in targets)
            {
                await _connectionMonitor.CheckNowAsync(target.TargetKey).ConfigureAwait(true);
            }

            ConnectionOfferResultText = DescribeConnectionOfferResult(_connectionMonitor.Snapshots);
        }
        finally
        {
            IsBusy = false;
        }

        HasConnectionOfferResult = ConnectionOfferResultText.Length > 0;
    }

    /// <summary>Cierra la oferta sin comprobar nada.</summary>
    [RelayCommand]
    private void DismissConnectionOffer()
    {
        HasConnectionOffer = false;
        HasConnectionOfferResult = false;
        ConnectionOfferResultText = string.Empty;
    }

    /// <summary>Una línea por destino con el estado informado y su motivo cuando corresponde.</summary>
    private static string DescribeConnectionOfferResult(IReadOnlyList<ConnectionTargetSnapshot> snapshots) =>
        string.Join(
            Environment.NewLine,
            snapshots.Select(snapshot =>
            {
                string state = ConnectionStatusText.For(snapshot.Status);
                string? reason = snapshot.LastResult?.Reason;

                return string.IsNullOrWhiteSpace(reason)
                    ? $"{snapshot.TargetName}: {state}."
                    : $"{snapshot.TargetName}: {state} — {reason}.";
            }));

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

    private async Task RevalidateAllAsync()
    {
        foreach (SettingsSectionViewModel section in Sections)
        {
            await section.RevalidateAllAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Publica si la configuración obligatoria está completa; el gate sólo se abre con todo válido.</summary>
    private void PublishReadiness()
    {
        bool valid = !HasStorageError
            && !Credentials.HasStorageError
            && !IsBusy
            && AllFields.All(field => field.IsValid && !field.IsValidating)
            && Credentials.AllowsStart;

        _gate.Report(valid);
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

    partial void OnIsBusyChanged(bool value) => PublishReadiness();
}
