using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Presentation.ViewModels.Config;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Seguimiento de cambios y confirmación modal de la pantalla Configuración (§2.9.2 r10-r13): el botón
/// Guardar se habilita sólo con un cambio real respecto de lo almacenado y un guardado que deja la
/// configuración incompleta para operar pide confirmación antes de escribir.
/// </summary>
public sealed partial class SettingsViewModel
{
    /// <summary>Título del aviso modal de §2.9.2 r12.</summary>
    public const string IncompleteSaveTitle = "Configuración incompleta";

    /// <summary>
    /// Texto del aviso modal de §2.9.2 r12: la información se guarda, pero sin todos los campos
    /// obligatorios el aplicativo no va a poder ejecutarse correctamente.
    /// </summary>
    public const string IncompleteSaveMessage =
        "La información se va a guardar, pero todavía falta completar campos obligatorios. " +
        "Sin todos los campos obligatorios completos, el aplicativo no va a poder ejecutarse correctamente. " +
        "¿Deseas guardar de todos modos?";

    /// <summary>Estado informado cuando el analista cancela el aviso: no se escribió nada.</summary>
    public const string SaveCancelledMessage =
        "No se guardó la configuración: los cambios quedan en pantalla.";

    /// <summary>§2.9.2 r10: verdadero sólo si algún valor difiere del almacenado.</summary>
    public bool HasChanges => AllFields.Any(field => field.HasValueChanged) || Proxy.HasProxyChanged;

    /// <summary>§2.9.2 r10: el botón Guardar sólo se habilita con un cambio real.</summary>
    public bool CanSave => HasChanges && !IsBusy;

    /// <summary>
    /// S18 y §2.9.2 r12: la configuración alcanza para operar sólo con todos los campos válidos y la
    /// credencial de correo configurada (Anexo D.2). Es el mismo estado que gobierna el botón INICIAR.
    /// </summary>
    private bool IsConfigurationCompleteForOperation =>
        !HasStorageError
        && !Credentials.HasStorageError
        && AllFields.All(field => field.IsValid && !field.IsValidating)
        && Credentials.AllowsStart;

    /// <summary>§2.9.2 r12: aviso modal previo a persistir una configuración incompleta para operar.</summary>
    private bool ConfirmIncompleteSave() => _confirmationDialog.Confirm(IncompleteSaveTitle, IncompleteSaveMessage);

    /// <summary>
    /// Fija la referencia de comparación: los valores recién cargados o recién guardados. Deja además los
    /// campos en sólo lectura (§2.9.2 r14).
    /// </summary>
    private void AcceptStoredValues()
    {
        foreach (SettingsSectionViewModel section in Sections)
        {
            section.AcceptStoredValues();
        }

        NotifySaveStateChanged();
    }

    private void NotifySaveStateChanged()
    {
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void OnFieldValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ValidatedFieldViewModel.Value)
            or nameof(ValidatedFieldViewModel.HasValueChanged))
        {
            NotifySaveStateChanged();
        }
    }

    private void OnProxyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProxySettingsViewModel.IsProxyEnabled)
            or nameof(ProxySettingsViewModel.HasProxyChanged))
        {
            NotifySaveStateChanged();
        }
    }
}
