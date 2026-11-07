using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Vista de sólo lectura de un lote con sus ejecuciones y fases (§3.1 a §3.3 del diseño de datos). Son
/// instantáneas: cada consulta abre su propio contexto y nada queda enganchado al contexto.
/// </summary>
public sealed record BatchStateSnapshot(
    int BatchId,
    string Code,
    int PeriodYear,
    int PeriodMonth,
    BatchStatus Status,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<ExecutionSnapshot> Executions)
{
    /// <summary>Última ejecución del lote, que es la que se continúa al reanudar (§2.8 regla 4).</summary>
    public ExecutionSnapshot? LastExecution =>
        Executions.Count == 0 ? null : Executions[^1];
}

/// <summary>Vista de una ejecución o reanudación de un lote (§3.2).</summary>
public sealed record ExecutionSnapshot(
    int ExecutionId,
    int Number,
    BatchStatus Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    DateTime? LastCheckpointAtUtc,
    int? ResumedFromExecutionId,
    IReadOnlyList<PhaseSnapshot> Phases);

/// <summary>Vista del estado de una de las cinco fases dentro de una ejecución (§3.3).</summary>
public sealed record PhaseSnapshot(int Phase, PhaseStatus Status, int ProcessedCount, int? TotalCount);

/// <summary>Fila resumida de un lote, para listar el historial sin sobrescribirlo (§2.8 reglas 9 y 10).</summary>
public sealed record BatchSummary(
    int BatchId,
    string Code,
    int PeriodYear,
    int PeriodMonth,
    BatchStatus Status,
    DateTime CreatedAtUtc,
    int ExecutionCount);

/// <summary>
/// Ejecución interrumpida que puede reanudarse: el último intento en uno de los estados recuperables del
/// §5 del diseño de datos (<c>EJECUTANDO</c>, <c>PAUSADO_POR_INCONSISTENCIA</c>, <c>LISTO_PARA_REANUDAR</c>).
/// </summary>
public sealed record RecoverableExecution(
    int ExecutionId,
    int BatchId,
    string BatchCode,
    int Number,
    BatchStatus Status);

/// <summary>Pedido de creación de un lote con su primera ejecución (§2.8).</summary>
public sealed record BatchCreationRequest(int PeriodYear, int PeriodMonth, string CreatedBy, string? Notes = null);

/// <summary>Lote y primera ejecución creados.</summary>
public sealed record BatchCreated(int BatchId, string Code, int ExecutionId, int ExecutionNumber);

/// <summary>Ejecución creada por un inicio o por una reanudación.</summary>
public sealed record ExecutionAppended(
    int BatchId,
    int ExecutionId,
    int ExecutionNumber,
    int? ResumedFromExecutionId);
