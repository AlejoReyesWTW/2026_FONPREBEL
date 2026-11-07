using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Presentation.ViewModels.Execution;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Acciones de la pantalla Ejecución (ficha V1, §2.18): qué habilita cada botón, qué explica su tooltip y
/// qué hace cada uno cuando el analista lo pulsa.
/// </summary>
/// <remarks>
/// <para>
/// El gate del Anexo D se decide en <see cref="ExecutionViewModel.CanStart"/>; acá sólo se traduce a texto y
/// se atiende el clic. Ninguna acción arranca el proceso todavía: el pipeline de las fases 1 a 5 es la tarea
/// B7, así que INICIAR y CANCELAR explican su estado en lugar de no hacer nada.
/// </para>
/// <para>
/// REANUDAR sí hace trabajo real: valida la recuperación del lote interrumpido con
/// <see cref="FonPrebel.Application.Interfaces.IBatchResumeService"/> y deja el lote en el estado que decide
/// el servicio. No reanuda la ejecución.
/// </para>
/// </remarks>
public sealed partial class ExecutionViewModel
{
    /// <summary>Por qué está deshabilitado INICIAR, para el tooltip que se muestra también deshabilitado.</summary>
    public string StartToolTip => !_gate.IsConfigurationComplete
        ? IncompleteConfigurationWarning
        : IsBatchRunning
            ? ExecutionNotices.StartBlockedByRunning
            : ExecutionNotices.StartReady;

    /// <summary>REANUDAR sólo se muestra si hay un lote interrumpido que pueda reanudarse (§2.18).</summary>
    public bool CanResume => IsResumeAvailable && !IsBusy;

    /// <summary>Qué hace REANUDAR hoy: valida la recuperación; la continuación es del bloque B.</summary>
    public string ResumeToolTip => IsResumeAvailable
        ? ExecutionNotices.ResumeExplanation
        : ExecutionNotices.ResumeUnavailable;

    /// <summary>CANCELAR sólo se muestra si hay un lote en ejecución.</summary>
    public bool CanCancel => IsBatchRunning && !IsBusy;

    /// <summary>Por qué está deshabilitado CANCELAR o qué hace cuando está habilitado.</summary>
    public string CancelToolTip => IsBatchRunning
        ? ExecutionNotices.CancelExplanation
        : ExecutionNotices.CancelUnavailable;

    /// <summary>INICIAR todavía no arranca el lote: deja dicho el motivo en vez de no hacer nada.</summary>
    [RelayCommand]
    private void Start() => ShowNotice((ExecutionNotices.StartPending, InfoBarSeverity.Informational));

    /// <summary>CANCELAR todavía no cancela el lote: deja dicho el motivo en vez de no hacer nada.</summary>
    [RelayCommand]
    private void Cancel() => ShowNotice((ExecutionNotices.CancelPending, InfoBarSeverity.Warning));

    /// <summary>
    /// Valida la recuperación del lote interrumpido (Anexo F.7) y vuelve a leer el estado. No reanuda: la
    /// continuación del proceso es una acción del bloque B, así que acá sólo se deja el lote listo.
    /// </summary>
    [RelayCommand]
    private async Task ResumeAsync()
    {
        if (_disposed || _reader is not { CanValidateRecovery: true } reader)
        {
            return;
        }

        CancellationToken token = BeginLoad();
        IsBusy = true;

        try
        {
            ResumeRun run = await Task
                .Run(() => reader.ValidateThenLoadAsync(token), token)
                .ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Apply(run.State);
            ReadErrorText = string.Empty;
            HasReadError = false;
            ShowNotice(ExecutionNotices.ResumeOutcome(run.Validation));
            LastRefreshText = $"Recuperación validada a las {ExecutionLabels.Time(DateTime.Now)}.";
        }
        catch (OperationCanceledException)
        {
            // La vista se cerró o se pidió otro refresco: no se toca el aviso.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ReadErrorText = $"No se pudo validar la recuperación: {exception.Message}";
            HasReadError = true;
            HasNotice = false;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>Publica el aviso de la última acción con su tono.</summary>
    private void ShowNotice((string Text, InfoBarSeverity Severity) notice)
    {
        NoticeSeverity = notice.Severity;
        NoticeText = notice.Text;
        HasNotice = notice.Text.Length > 0;
    }
}
