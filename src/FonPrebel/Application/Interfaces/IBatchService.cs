using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Ciclo de vida de un lote y de sus ejecuciones (§2.8, Anexo F.8). Crear un lote crea su ejecución 1 con
/// las cinco fases en <see cref="PhaseStatus.PENDIENTE"/>; reanudar <b>nunca</b> crea un lote nuevo.
/// </summary>
/// <remarks>
/// Cada llamada abre su propio contexto desde la factoría (S3, §6 del diseño de datos): no hay contexto
/// compartido entre operaciones ni entre hilos.
/// </remarks>
public interface IBatchService
{
    /// <summary>
    /// Crea un lote en estado <see cref="BatchStatus.LISTO"/> con su ejecución número 1 y las cinco fases
    /// en <see cref="PhaseStatus.PENDIENTE"/>. Un identificador que choca con uno existente se regenera:
    /// dos lotes del mismo periodo no colisionan (§2.8 regla 1).
    /// </summary>
    Task<BatchOperationResult<BatchCreated>> CreateAsync(
        BatchCreationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Inicia una ejecución creada: la ejecución y su lote pasan a <see cref="BatchStatus.EJECUTANDO"/>.</summary>
    Task<BatchOperationResult<ExecutionAppended>> StartExecutionAsync(
        int executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reanuda un lote ya validado: agrega una ejecución con <c>Number = n+1</c> y
    /// <c>ResumedFromExecutionId</c> apuntando a la anterior, del mismo lote (§2.8 reglas 4 y 5). Exige que
    /// el lote esté en <see cref="BatchStatus.LISTO_PARA_REANUDAR"/> y revalida acá, sin confiar en una
    /// validación previa, que su última ejecución sea la interrumpida: un lote cerrado o cuya última
    /// ejecución no es recuperable se rechaza sin tocar nada (Anexo F.7 y F.8).
    /// </summary>
    Task<BatchOperationResult<ExecutionAppended>> ResumeAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra una ejecución con un estado terminal (<see cref="BatchStatus.COMPLETADO"/> o
    /// <see cref="BatchStatus.CANCELADO"/>) y deja el lote en ese mismo estado.
    /// </summary>
    Task<BatchOperationResult> CloseExecutionAsync(
        int executionId,
        BatchStatus terminalStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado de una ejecución y de su lote a la vez, en una sola transacción, por ejemplo a
    /// <see cref="BatchStatus.VALIDANDO_RECUPERACION"/>. Anexo F.3 y F.8: el cambio queda registrado en la
    /// ejecución y en el lote, sin dejar una ejecución que diga seguir corriendo cuando está detenida.
    /// </summary>
    Task<BatchOperationResult> SetStateAsync(
        int executionId,
        BatchStatus status,
        CancellationToken cancellationToken = default);
}
