using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Validación de recuperación de una ejecución interrumpida: la secuencia exacta del §5 del diseño de
/// datos y del Anexo F.7 y F.9.
/// </summary>
/// <remarks>
/// <para>
/// El orden no se altera: buscar la última ejecución en estado recuperable, pasar el lote a
/// <see cref="BatchStatus.VALIDANDO_RECUPERACION"/>, recalcular y comparar las huellas de los archivos
/// registrados y dejar el lote en <see cref="BatchStatus.LISTO_PARA_REANUDAR"/> o en
/// <see cref="BatchStatus.PAUSADO_POR_INCONSISTENCIA"/>.
/// </para>
/// <para>
/// Este servicio <b>no</b> reanuda: sólo valida y deja el estado. Nunca reescribe el archivo del analista ni
/// crea un lote nuevo, y nunca reanuda automáticamente (Anexo F.7).
/// </para>
/// </remarks>
public interface IBatchResumeService
{
    /// <summary>
    /// Valida las huellas de la última ejecución interrumpida y deja el lote listo para reanudar o pausado
    /// por inconsistencia, con el detalle de cada archivo comparado.
    /// </summary>
    Task<BatchResumeValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de la validación de recuperación. <see cref="BatchStatus"/> es el estado en el que quedó el
/// lote; <see cref="Comparisons"/> trae una entrada por archivo registrado, con su motivo. Cuando la
/// validación termina pausando el lote, <see cref="Outcome"/> es
/// <see cref="BatchOutcome.PausedByInconsistency"/> y no un éxito: pausar por inconsistencia no es haber
/// validado bien (Anexo F.5).
/// </summary>
public sealed record BatchResumeValidationResult(
    BatchOutcome Outcome,
    int? BatchId,
    string? BatchCode,
    int? ExecutionId,
    BatchStatus? BatchStatus,
    string? Reason,
    IReadOnlyList<InputFingerprintComparison> Comparisons)
{
    /// <summary>
    /// Indica si el lote quedó <see cref="Domain.Enums.BatchStatus.LISTO_PARA_REANUDAR"/> con todas las
    /// huellas coincidentes. No reanuda nada: la reanudación es una acción explícita del analista.
    /// </summary>
    public bool CanResume =>
        Outcome == BatchOutcome.Succeeded && BatchStatus == Domain.Enums.BatchStatus.LISTO_PARA_REANUDAR;
}
