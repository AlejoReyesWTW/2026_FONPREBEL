using FonPrebel.Application.Interfaces;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.ViewModels.Execution;

/// <summary>
/// Textos que la pantalla Ejecución muestra en su estado vacío, en el aviso del progreso general pendiente,
/// en los tooltips de las tres acciones y en el resultado de la validación de recuperación.
/// </summary>
/// <remarks>
/// Se centralizan acá para que la vista no componga mensajes y para que el ViewModel quede enfocado en el
/// estado. Ninguno de estos textos afirma datos de negocio: describen el estado de la pantalla o el motivo
/// por el que una acción está bloqueada o pendiente.
/// </remarks>
internal static class ExecutionNotices
{
    /// <summary>Estado vacío: no hay lote del que leer nada.</summary>
    internal const string EmptyStateTitle = "Todavía no hay ningún lote en curso.";

    /// <summary>Qué va a aparecer en la pantalla cuando exista un lote.</summary>
    internal const string EmptyStateDetail =
        "Cuando inicies el proceso vas a ver el lote activo, el avance de las cinco fases con sus cifras reales y el resultado de cada una.";

    /// <summary>§2.16 y §5.6: la fórmula del progreso general no está definida en el spec.</summary>
    internal const string GlobalProgressPending =
        "La fórmula del progreso general todavía no está definida en la especificación (§2.16, §5.6), así que no se muestra un porcentaje global: el avance real se lee en las cinco barras por fase.";

    /// <summary>Aviso al pulsar INICIAR: el arranque del pipeline es la tarea B7.</summary>
    internal const string StartPending =
        "El arranque del lote todavía no está implementado: las fases 1 a 5 llegan con el bloque B. Esta pantalla ya muestra el estado persistido y se refresca sin bloquear la interfaz.";

    /// <summary>Aviso al pulsar CANCELAR: la cancelación del pipeline es la tarea B7.</summary>
    internal const string CancelPending =
        "La cancelación del lote todavía no está implementada: llega con el pipeline de las fases 1 a 5 (bloque B).";

    /// <summary>Tooltip de INICIAR cuando ya hay un lote en ejecución.</summary>
    internal const string StartBlockedByRunning =
        "Ya hay un lote en ejecución: no se inicia otro hasta que termine o se cancele.";

    /// <summary>Tooltip de INICIAR cuando el gate del Anexo D está abierto.</summary>
    internal const string StartReady =
        "Abre un lote nuevo para el periodo en curso. El arranque del proceso llega con el bloque B.";

    /// <summary>Tooltip de REANUDAR cuando hay un lote interrumpido.</summary>
    internal const string ResumeExplanation =
        "Valida las entradas del lote interrumpido y deja el lote listo para reanudar. La continuación del proceso llega con el bloque B.";

    /// <summary>Tooltip de REANUDAR cuando no hay nada que reanudar.</summary>
    internal const string ResumeUnavailable = "No hay ningún lote interrumpido que se pueda reanudar.";

    /// <summary>Tooltip de CANCELAR cuando hay un lote en ejecución.</summary>
    internal const string CancelExplanation =
        "Deja el lote en un estado terminal consistente (Anexo F). La cancelación del proceso llega con el bloque B.";

    /// <summary>Tooltip de CANCELAR cuando no hay nada en ejecución.</summary>
    internal const string CancelUnavailable = "No hay ningún lote en ejecución para cancelar.";

    /// <summary>
    /// Traduce el resultado de <see cref="IBatchResumeService.ValidateAsync"/> a un aviso con su tono. Leer
    /// "pausado por inconsistencia" como un éxito sería afirmar algo que no pasó (Anexo F.5).
    /// </summary>
    internal static (string Text, InfoBarSeverity Severity) ResumeOutcome(BatchResumeValidationResult validation)
    {
        string batch = string.IsNullOrWhiteSpace(validation.BatchCode)
            ? "El lote"
            : $"El lote {validation.BatchCode}";

        if (validation.CanResume)
        {
            return (
                $"{batch} quedó listo para reanudar: las huellas de las entradas de la ejecución coinciden. La continuación del proceso llega con el bloque B.",
                InfoBarSeverity.Success);
        }

        if (validation.Outcome == BatchOutcome.PausedByInconsistency)
        {
            string reason = string.IsNullOrWhiteSpace(validation.Reason)
                ? "una entrada cambió respecto de la huella registrada"
                : validation.Reason;

            return (
                $"{batch} quedó pausado por inconsistencia: {reason}. Revisá las entradas antes de volver a intentarlo.",
                InfoBarSeverity.Warning);
        }

        string detail = string.IsNullOrWhiteSpace(validation.Reason)
            ? "no hay ninguna ejecución interrumpida para validar"
            : validation.Reason;

        return ($"No se pudo dejar el lote listo para reanudar: {detail}.", InfoBarSeverity.Error);
    }
}
