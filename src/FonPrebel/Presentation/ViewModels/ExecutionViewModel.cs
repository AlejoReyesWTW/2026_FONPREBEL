using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Application.Services;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Ejecución (V1): operar el lote y seguir el avance de las cinco fases. En esta entrega sólo
/// existe el botón INICIAR con el gate del Anexo D: el arranque del lote es la tarea A7.
/// </summary>
public sealed partial class ExecutionViewModel : SectionViewModel
{
    /// <summary>
    /// Anexo D: aviso literal cuando la configuración obligatoria no está completa. El texto no se
    /// reescribe en la vista.
    /// </summary>
    public const string IncompleteConfigurationWarning =
        "Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso.";

    private readonly ConfigurationReadinessGate _gate;

    [ObservableProperty]
    private bool _isBatchRunning;

    public ExecutionViewModel(ConfigurationReadinessGate gate)
        : base("execution", "Ejecución",
               "Acá el analista inicia, reanuda y observa el proceso: indicador de fases, barra global y una barra por fase.")
    {
        ArgumentNullException.ThrowIfNull(gate);

        _gate = gate;
        _gate.Changed += OnReadinessChanged;
    }

    /// <summary>
    /// S18 y Anexo D: el botón INICIAR sólo se habilita con toda la configuración obligatoria válida y
    /// sin lote en ejecución.
    /// </summary>
    public bool CanStart => _gate.IsConfigurationComplete && !IsBatchRunning;

    /// <summary>Muestra el aviso de configuración incompleta mientras el botón está deshabilitado.</summary>
    public bool ShowsIncompleteConfigurationWarning => !CanStart;

    /// <summary>Texto literal del aviso del Anexo D, para enlazarlo en la vista.</summary>
    public string IncompleteConfigurationWarningText => IncompleteConfigurationWarning;

    private void OnReadinessChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(ShowsIncompleteConfigurationWarning));
    }

    partial void OnIsBatchRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(ShowsIncompleteConfigurationWarning));
    }
}
