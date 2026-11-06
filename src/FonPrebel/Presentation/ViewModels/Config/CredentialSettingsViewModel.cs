using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Presentation.ViewModels.Config;

/// <summary>
/// Sección Credenciales (§3 del diseño de configuración): muestra el estado del secreto, permite ver lo
/// que se escribe y elige el modo edición según ese estado: alta cuando no hay credencial, reingreso
/// cuando la guardada no se descifra en este perfil y reemplazo con contraseña anterior cuando hay una
/// credencial utilizable.
/// </summary>
/// <remarks>
/// La contraseña guardada nunca se carga, se muestra ni se registra: no hay ninguna propiedad que la
/// exponga. Toda operación se informa como <b>guardado, sin validar en el servidor</b> porque el
/// mecanismo de autenticación sigue pendiente (§2.10.1); una base inaccesible se muestra como aviso.
/// </remarks>
public sealed partial class CredentialSettingsViewModel : ObservableObject
{
    /// <summary>Confirmación del guardado: nunca se afirma que la credencial quedó validada.</summary>
    public const string SavedWithoutValidationMessage = "Credencial guardada, sin validar en el servidor.";

    /// <summary>Aviso cuando el secreto guardado no se descifra en este perfil de Windows.</summary>
    public const string NotRecoverableWarning =
        "La credencial guardada no se puede descifrar en este perfil de Windows. Hay que reingresar la credencial para seguir.";

    /// <summary>Motivo del rechazo del alta cuando ya hay una credencial utilizable.</summary>
    public const string AlreadyConfiguredMessage =
        "Ya hay una credencial guardada y utilizable: hay que reemplazarla con la contraseña anterior.";

    /// <summary>Motivo del rechazo del reingreso cuando la credencial guardada se descifra.</summary>
    public const string PreviousPasswordRequiredMessage =
        "La credencial guardada se puede descifrar en este perfil: el cambio exige la contraseña anterior.";

    /// <summary>Aviso cuando la base local no responde.</summary>
    public const string StorageErrorMessage =
        "No se pudo acceder a la credencial de correo porque la base local no está disponible.";

    private readonly IEmailCredentialService _credentialService;

    [ObservableProperty]
    private EmailCredentialState _state;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusError;

    [ObservableProperty]
    private bool _hasStorageError;

    [ObservableProperty]
    private string _previousPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmationPassword = string.Empty;

    public CredentialSettingsViewModel(IEmailCredentialService credentialService)
    {
        ArgumentNullException.ThrowIfNull(credentialService);

        _credentialService = credentialService;
    }

    /// <summary>Se dispara cuando cambia el estado de la credencial, para recalcular el gate de INICIAR.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Indica si hay una credencial guardada y descifrable.</summary>
    public bool HasCredential => State == EmailCredentialState.Available;

    /// <summary>Indica si el secreto guardado no se descifra en este perfil de Windows.</summary>
    public bool IsNotRecoverable => State == EmailCredentialState.NotRecoverable;

    /// <summary>Indica si todavía no hay credencial: el modo edición pide la contraseña dos veces.</summary>
    public bool IsFirstRegistration => State == EmailCredentialState.NotConfigured;

    /// <summary>Sólo una credencial utilizable exige la contraseña anterior para cambiarse (§3.2).</summary>
    public bool RequiresPreviousPassword => State == EmailCredentialState.Available;

    /// <summary>Aviso visible cuando la credencial guardada no se descifra en este perfil (§2.10).</summary>
    public bool ShowsNotRecoverableWarning => IsNotRecoverable;

    /// <summary>Texto del aviso de credencial no recuperable, sin ningún dato del secreto.</summary>
    public string ReentryWarningText => NotRecoverableWarning;

    /// <summary>
    /// El gate de INICIAR exige una credencial configurada: sin secreto o con un secreto que no se
    /// descifra la configuración obligatoria está incompleta (S18, Anexo D).
    /// </summary>
    public bool AllowsStart => HasCredential;

    /// <summary>Texto del estado del secreto; nunca incluye la contraseña.</summary>
    public string StateText => State switch
    {
        EmailCredentialState.Available =>
            "Credencial configurada. La contraseña guardada no se muestra; se puede reemplazar.",
        EmailCredentialState.NotRecoverable => NotRecoverableWarning,
        _ => "No hay credencial configurada: hay que registrarla para poder iniciar el proceso.",
    };

    /// <summary>Acción que ofrece el botón cuando no se está editando, según el estado del secreto.</summary>
    public string EditButtonText => State switch
    {
        EmailCredentialState.Available => "Reemplazar",
        EmailCredentialState.NotRecoverable => "Reingresar",
        _ => "Registrar",
    };

    /// <summary>Etiqueta del campo de contraseña que se va a guardar.</summary>
    public string NewPasswordLabel => IsFirstRegistration ? "Contraseña" : "Contraseña nueva";

    /// <summary>Etiqueta del campo de confirmación, según el flujo que corresponde.</summary>
    public string ConfirmationLabel =>
        RequiresPreviousPassword ? "Confirmación de la contraseña nueva" : "Confirmar contraseña";

    /// <summary>Texto del botón de guardado, según el flujo que corresponde.</summary>
    public string SaveButtonText => IsFirstRegistration ? "Registrar credencial" : "Guardar credencial";

    /// <summary>
    /// Campo enmascarado de la credencial guardada. Siempre vacío: el secreto no se carga ni se precarga.
    /// </summary>
    public string StoredCredentialMask => string.Empty;

    /// <summary>Indica si se puede entrar en modo edición.</summary>
    public bool CanBeginEdit => !IsEditing && !IsBusy;

    /// <summary>Lee el estado de la credencial sin propagar excepciones de almacenamiento.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EmailCredentialStatus status = await _credentialService
                .GetStateAsync(cancellationToken)
                .ConfigureAwait(true);

            HasStorageError = false;
            ApplyState(status.State);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportStorageError();
        }
    }

    /// <summary>Alterna el modo edición de los tres campos.</summary>
    [RelayCommand]
    private void BeginEdit()
    {
        if (!CanBeginEdit)
        {
            return;
        }

        PreviousPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmationPassword = string.Empty;
        StatusMessage = string.Empty;
        HasStatusError = false;
        IsEditing = true;
        OnPropertyChanged(nameof(CanBeginEdit));
    }

    /// <summary>Cancela el modo edición y descarta lo escrito.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        PreviousPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmationPassword = string.Empty;
        IsEditing = false;
        OnPropertyChanged(nameof(CanBeginEdit));
    }

    /// <summary>
    /// §3.2: elige el flujo según el estado del secreto, valida los campos que ese flujo pide y guarda de
    /// forma atómica. Sin confirmación coincidente no se llama al servicio. Devuelve verdadero sólo si el
    /// guardado se completó.
    /// </summary>
    public async Task<bool> ReplaceAsync(CancellationToken cancellationToken = default)
    {
        ValidationResult validation = ValidateEntry();
        if (!validation.IsValid)
        {
            SetStatus(validation.Message, isError: true);

            return false;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanBeginEdit));

        try
        {
            if (RequiresPreviousPassword)
            {
                bool previousMatches;
                try
                {
                    previousMatches = await _credentialService
                        .VerifyAsync(PreviousPassword, cancellationToken)
                        .ConfigureAwait(true);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    ReportStorageError();

                    return false;
                }

                if (!previousMatches)
                {
                    SetStatus(
                        $"{CredentialValidation.PreviousParameter}: no coincide con la credencial guardada.",
                        isError: true);

                    return false;
                }
            }

            CredentialReplaceResult result;
            try
            {
                result = await SubmitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ReportStorageError();

                return false;
            }

            HasStorageError = false;

            return await HandleReplaceResultAsync(result, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanBeginEdit));
        }
    }

    /// <summary>Ejecuta el reemplazo desde la vista.</summary>
    [RelayCommand]
    private Task Replace() => ReplaceAsync();

    partial void OnStateChanged(EmailCredentialState value)
    {
        OnPropertyChanged(nameof(HasCredential));
        OnPropertyChanged(nameof(IsNotRecoverable));
        OnPropertyChanged(nameof(IsFirstRegistration));
        OnPropertyChanged(nameof(RequiresPreviousPassword));
        OnPropertyChanged(nameof(ShowsNotRecoverableWarning));
        OnPropertyChanged(nameof(AllowsStart));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(EditButtonText));
        OnPropertyChanged(nameof(NewPasswordLabel));
        OnPropertyChanged(nameof(ConfirmationLabel));
        OnPropertyChanged(nameof(SaveButtonText));

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Valida sólo los campos que pide el flujo del estado; sin eso no se llama al servicio.</summary>
    private ValidationResult ValidateEntry()
    {
        if (RequiresPreviousPassword)
        {
            return CredentialValidation.ValidateAll(PreviousPassword, NewPassword, ConfirmationPassword);
        }

        ValidationResult result = CredentialValidation.ValidateNew(NewPassword);
        if (!result.IsValid)
        {
            return WithVisibleLabels(result);
        }

        return WithVisibleLabels(CredentialValidation.ValidateConfirmation(NewPassword, ConfirmationPassword));
    }

    /// <summary>Cambia el nombre del parámetro por la etiqueta visible del flujo de alta o reingreso.</summary>
    private ValidationResult WithVisibleLabels(ValidationResult result)
    {
        if (result.IsValid || RequiresPreviousPassword)
        {
            return result;
        }

        string newPrefix = CredentialValidation.NewParameter;
        if (result.Message.StartsWith(newPrefix + ":", StringComparison.Ordinal))
        {
            return new ValidationResult(false, NewPasswordLabel + result.Message[newPrefix.Length..]);
        }

        string confirmationPrefix = CredentialValidation.ConfirmationParameter;
        if (result.Message.StartsWith(confirmationPrefix + ":", StringComparison.Ordinal))
        {
            return new ValidationResult(false, ConfirmationLabel + result.Message[confirmationPrefix.Length..]);
        }

        return result;
    }

    private Task<CredentialReplaceResult> SubmitAsync(CancellationToken cancellationToken) => State switch
    {
        EmailCredentialState.NotConfigured => _credentialService.TryRegisterAsync(NewPassword, cancellationToken),
        EmailCredentialState.NotRecoverable => _credentialService.TryReenterAsync(NewPassword, cancellationToken),
        _ => _credentialService.TryReplaceAsync(PreviousPassword, NewPassword, cancellationToken),
    };

    private async Task<bool> HandleReplaceResultAsync(
        CredentialReplaceResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case CredentialReplaceOutcome.Replaced:
                PreviousPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmationPassword = string.Empty;
                IsEditing = false;
                OnPropertyChanged(nameof(CanBeginEdit));
                SetStatus(SavedWithoutValidationMessage, isError: false);
                await RefreshAsync(cancellationToken).ConfigureAwait(true);

                return true;

            case CredentialReplaceOutcome.NotConfigured:
                SetStatus(
                    "No hay una credencial guardada que reingresar: hay que registrarla primero.",
                    isError: true);

                return false;

            case CredentialReplaceOutcome.AlreadyConfigured:
                SetStatus(AlreadyConfiguredMessage, isError: true);
                await RefreshAsync(cancellationToken).ConfigureAwait(true);

                return false;

            case CredentialReplaceOutcome.PreviousPasswordRequired:
                SetStatus(PreviousPasswordRequiredMessage, isError: true);
                await RefreshAsync(cancellationToken).ConfigureAwait(true);

                return false;

            case CredentialReplaceOutcome.NotRecoverable:
                SetStatus(NotRecoverableWarning, isError: true);
                await RefreshAsync(cancellationToken).ConfigureAwait(true);

                return false;

            case CredentialReplaceOutcome.PreviousPasswordMismatch:
                SetStatus(
                    $"{CredentialValidation.PreviousParameter}: no coincide con la credencial guardada.",
                    isError: true);

                return false;

            case CredentialReplaceOutcome.NewSecretRejected:
                SetStatus(
                    $"{CredentialValidation.NewParameter}: no se pudo cifrar ni guardar la contraseña nueva.",
                    isError: true);

                return false;

            default:
                ReportStorageError();

                return false;
        }
    }

    private void ApplyState(EmailCredentialState state) => State = state;

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        HasStatusError = isError;
    }

    private void ReportStorageError()
    {
        HasStorageError = true;
        SetStatus(StorageErrorMessage, isError: true);
    }
}
