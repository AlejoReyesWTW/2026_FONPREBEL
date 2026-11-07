using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Persistencia de lotes y ejecuciones (§3.1 y §3.2 del diseño de datos). Las transiciones que mueven el
/// lote y su ejecución a la vez son atómicas: se resuelven en un único <c>SaveChanges</c> con su propio
/// contexto, nunca con un contexto compartido.
/// </summary>
public interface IBatchStore
{
    /// <summary>Identificadores ya usados en un periodo, para no repetir ninguno (§2.8 regla 1).</summary>
    Task<IReadOnlyList<string>> ListBatchCodesAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda un lote con sus ejecuciones y fases en una sola transacción. Un identificador repetido se
    /// informa como <see cref="BatchOutcome.DuplicateCode"/> y no se guarda nada.
    /// </summary>
    Task<BatchOperationResult<BatchCreated>> AddBatchAsync(
        Batch batch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pasa la ejecución y su lote a <see cref="BatchStatus.EJECUTANDO"/> en una sola transacción. Sólo se
    /// acepta desde <see cref="BatchStatus.LISTO"/>.
    /// </summary>
    Task<BatchOperationResult<ExecutionAppended>> StartExecutionAsync(
        int executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega la ejecución <c>n+1</c> del lote, con <c>ResumedFromExecutionId</c> apuntando a la última,
    /// sus cinco fases en <see cref="PhaseStatus.PENDIENTE"/>, las huellas de entrada copiadas de la
    /// ejecución anterior y el lote en <see cref="BatchStatus.EJECUTANDO"/> (§2.8 regla 5, Anexo F.6).
    /// La ejecución anterior queda cerrada con un estado terminal para que deje de ser recuperable
    /// (Anexo F.8); el lote no se cierra, porque el trabajo continúa en la ejecución nueva.
    /// </summary>
    /// <remarks>
    /// La condición de que el lote esté <see cref="BatchStatus.LISTO_PARA_REANUDAR"/> y de que su última
    /// ejecución sea la interrumpida la hace cumplir <see cref="IBatchService"/>, que es la dueña de la regla
    /// del Anexo F.7 y F.8: acá no se reanuda un lote que no fue validado ni uno cerrado.
    /// </remarks>
    Task<BatchOperationResult<ExecutionAppended>> AddResumeExecutionAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra la ejecución y el lote con un estado terminal en una sola transacción.</summary>
    Task<BatchOperationResult> CloseExecutionAsync(
        int executionId,
        BatchStatus terminalStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado de la ejecución y el de su lote en una sola transacción. Dejar los dos en el mismo
    /// estado evita que una ejecución detenida siga figurando como <see cref="BatchStatus.EJECUTANDO"/> al
    /// recuperar el estado persistido (Anexo F.3 y F.8).
    /// </summary>
    Task<BatchOperationResult> SetStateAsync(
        int executionId,
        BatchStatus status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Consulta de sólo lectura del estado persistido de los lotes: el lote activo, el historial y el lote
/// reanudable. Es la entrada del §5 del diseño de datos al abrir la aplicación (Anexo F.7).
/// </summary>
public interface IBatchStateQuery
{
    /// <summary>Estado del lote indicado, con sus ejecuciones y fases; nulo si no existe.</summary>
    Task<BatchStateSnapshot?> GetBatchAsync(int batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lote activo: el más reciente que todavía no está en estado terminal
    /// (<see cref="BatchStatus.COMPLETADO"/> o <see cref="BatchStatus.CANCELADO"/>). Nulo si no hay ninguno.
    /// </summary>
    Task<BatchStateSnapshot?> GetActiveBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>Lotes guardados, del más nuevo al más viejo. Nada se borra ni se sobrescribe (§2.8 reglas 8 y 9).</summary>
    Task<IReadOnlyList<BatchSummary>> ListBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Última ejecución en estado recuperable —<see cref="BatchStatus.EJECUTANDO"/>,
    /// <see cref="BatchStatus.PAUSADO_POR_INCONSISTENCIA"/> o <see cref="BatchStatus.LISTO_PARA_REANUDAR"/>—
    /// con el lote al que pertenece. Sólo cuenta la última ejecución de cada lote y nunca un lote en estado
    /// terminal (<see cref="BatchStatus.COMPLETADO"/> o <see cref="BatchStatus.CANCELADO"/>). Nulo si no hay
    /// ninguna interrumpida (Anexo F.7 y F.8).
    /// </summary>
    Task<RecoverableExecution?> FindResumableAsync(CancellationToken cancellationToken = default);
}
