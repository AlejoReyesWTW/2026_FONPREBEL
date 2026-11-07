using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// S20 (Anexo F.7 y F.9) y §5 del diseño de datos: validación de recuperación de la última ejecución
/// interrumpida, en la secuencia exacta del diseño.
/// </summary>
/// <remarks>
/// <para>
/// 1) se busca la última ejecución en estado recuperable; 2) el lote pasa a
/// <see cref="BatchStatus.VALIDANDO_RECUPERACION"/>; 3) se recalculan y comparan las huellas registradas;
/// 4) si alguna difiere o no es verificable, el lote queda en
/// <see cref="BatchStatus.PAUSADO_POR_INCONSISTENCIA"/> con el motivo en el log y en el resultado; 5) si
/// todas coinciden, queda en <see cref="BatchStatus.LISTO_PARA_REANUDAR"/>.
/// </para>
/// <para>
/// No reanuda nada por su cuenta: el analista decide reanudar. Tampoco reescribe el archivo del analista ni
/// crea lotes (Anexo F.7).
/// </para>
/// </remarks>
public sealed class BatchResumeService : IBatchResumeService
{
    private readonly IBatchStateQuery _stateQuery;
    private readonly IBatchService _batchService;
    private readonly IInputFingerprintService _fingerprints;
    private readonly ILogger<BatchResumeService> _logger;

    public BatchResumeService(
        IBatchStateQuery stateQuery,
        IBatchService batchService,
        IInputFingerprintService fingerprints,
        ILogger<BatchResumeService> logger)
    {
        ArgumentNullException.ThrowIfNull(stateQuery);
        ArgumentNullException.ThrowIfNull(batchService);
        ArgumentNullException.ThrowIfNull(fingerprints);
        ArgumentNullException.ThrowIfNull(logger);

        _stateQuery = stateQuery;
        _batchService = batchService;
        _fingerprints = fingerprints;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchResumeValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        RecoverableExecution? execution = await _stateQuery
            .FindResumableAsync(cancellationToken)
            .ConfigureAwait(false);

        if (execution is null)
        {
            _logger.LogInformation("No hay ninguna ejecución interrumpida para validar: no se reanuda nada");

            return new BatchResumeValidationResult(
                BatchOutcome.ExecutionNotFound,
                null,
                null,
                null,
                null,
                "No hay ninguna ejecución interrumpida que se pueda reanudar.",
                Array.Empty<InputFingerprintComparison>());
        }

        BatchOperationResult validating = await _batchService
            .SetStateAsync(execution.ExecutionId, BatchStatus.VALIDANDO_RECUPERACION, cancellationToken)
            .ConfigureAwait(false);

        if (!validating.Succeeded)
        {
            _logger.LogWarning(
                "No se pudo poner el lote {Codigo} en validación de recuperación. {Motivo}: {Detalle}",
                execution.BatchCode,
                validating.Outcome,
                validating.Detail);

            return new BatchResumeValidationResult(
                validating.Outcome,
                execution.BatchId,
                execution.BatchCode,
                execution.ExecutionId,
                null,
                validating.Detail,
                Array.Empty<InputFingerprintComparison>());
        }

        BatchOperationResult<IReadOnlyList<InputFingerprintComparison>> verified = await _fingerprints
            .VerifyAsync(execution.ExecutionId, cancellationToken)
            .ConfigureAwait(false);

        if (!verified.Succeeded)
        {
            return await PauseAsync(execution, verified.Outcome, verified.Detail, Array.Empty<InputFingerprintComparison>())
                .ConfigureAwait(false);
        }

        IReadOnlyList<InputFingerprintComparison> comparisons = verified.Value!;
        string? problem = FindProblem(comparisons);

        if (problem is not null)
        {
            // Anexo F.5: pausar por inconsistencia no es un éxito; el resultado lo informa con su propio motivo.
            return await PauseAsync(execution, BatchOutcome.PausedByInconsistency, problem, comparisons)
                .ConfigureAwait(false);
        }

        BatchOperationResult ready = await _batchService
            .SetStateAsync(execution.ExecutionId, BatchStatus.LISTO_PARA_REANUDAR, cancellationToken)
            .ConfigureAwait(false);

        if (!ready.Succeeded)
        {
            return new BatchResumeValidationResult(
                ready.Outcome,
                execution.BatchId,
                execution.BatchCode,
                execution.ExecutionId,
                null,
                ready.Detail,
                comparisons);
        }

        _logger.LogInformation(
            "Lote {Codigo} validado: las {Archivos} huellas de entrada coinciden y la ejecución {Ejecucion} queda lista para reanudar",
            execution.BatchCode,
            comparisons.Count,
            execution.ExecutionId);

        return new BatchResumeValidationResult(
            BatchOutcome.Succeeded,
            execution.BatchId,
            execution.BatchCode,
            execution.ExecutionId,
            BatchStatus.LISTO_PARA_REANUDAR,
            null,
            comparisons);
    }

    /// <summary>
    /// Motivo por el que no se puede reanudar, o nulo si todo coincide. Sin huellas registradas no hay
    /// insumo verificado, y eso no es una coincidencia (Anexo F.4 y F.11).
    /// </summary>
    private static string? FindProblem(IReadOnlyList<InputFingerprintComparison> comparisons)
    {
        if (comparisons.Count == 0)
        {
            return "La ejecución no tiene huellas de archivos de entrada registradas: no se puede verificar " +
                   "que los insumos sean los mismos y no se reanuda.";
        }

        return comparisons
            .FirstOrDefault(comparison => comparison.Status != InputFingerprintStatus.Matches)
            ?.Reason;
    }

    /// <summary>Deja el lote pausado por inconsistencia y registra el motivo, sin reanudar ni reescribir nada.</summary>
    /// <remarks>
    /// La pausa se escribe sin el token de la llamada: es el estado seguro al que el proceso debe llegar
    /// aunque la validación se cancele a mitad de camino (Anexo F.5).
    /// </remarks>
    private async Task<BatchResumeValidationResult> PauseAsync(
        RecoverableExecution execution,
        BatchOutcome outcome,
        string? reason,
        IReadOnlyList<InputFingerprintComparison> comparisons)
    {
        string motivo = reason ?? "La validación de recuperación no pudo completarse.";

        BatchOperationResult paused = await _batchService
            .SetStateAsync(execution.ExecutionId, BatchStatus.PAUSADO_POR_INCONSISTENCIA, CancellationToken.None)
            .ConfigureAwait(false);

        if (paused.Succeeded)
        {
            _logger.LogWarning(
                "Reanudación bloqueada para el lote {Codigo}: {Motivo}",
                execution.BatchCode,
                motivo);

            return new BatchResumeValidationResult(
                outcome,
                execution.BatchId,
                execution.BatchCode,
                execution.ExecutionId,
                BatchStatus.PAUSADO_POR_INCONSISTENCIA,
                motivo,
                comparisons);
        }

        _logger.LogError(
            "No se pudo dejar el lote {Codigo} pausado por inconsistencia. {Motivo}: {Detalle}. Detalle de la validación: {Validacion}",
            execution.BatchCode,
            paused.Outcome,
            paused.Detail,
            motivo);

        return new BatchResumeValidationResult(
            paused.Outcome,
            execution.BatchId,
            execution.BatchCode,
            execution.ExecutionId,
            null,
            paused.Detail,
            comparisons);
    }
}
