using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Consulta de sólo lectura del estado persistido de los lotes (§3.1 a §3.3 del diseño de datos). Cada
/// consulta abre su propio contexto desde la factoría y devuelve instantáneas: nada queda enganchado.
/// </summary>
public sealed class BatchStateQuery : IBatchStateQuery
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<BatchStateQuery> _logger;

    public BatchStateQuery(IDbContextFactory<AppDbContext> contextFactory, ILogger<BatchStateQuery> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchStateSnapshot?> GetBatchAsync(int batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            Batch? batch = await WithHistory(context.Batches)
                .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
                .ConfigureAwait(false);

            return batch is null ? null : ToSnapshot(batch);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "leer el lote");

            return null;
        }
    }

    /// <inheritdoc />
    public async Task<BatchStateSnapshot?> GetActiveBatchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // COMPLETADO y CANCELADO son los estados terminales del Anexo F.8: un lote así ya no está activo.
            Batch? batch = await WithHistory(context.Batches)
                .Where(item => item.Status != BatchStatus.COMPLETADO && item.Status != BatchStatus.CANCELADO)
                .OrderByDescending(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return batch is null ? null : ToSnapshot(batch);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "leer el lote activo");

            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchSummary>> ListBatchesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            return await context.Batches
                .AsNoTracking()
                .OrderByDescending(item => item.Id)
                .Select(item => new BatchSummary(
                    item.Id,
                    item.Code,
                    item.PeriodYear,
                    item.PeriodMonth,
                    item.Status,
                    item.CreatedAtUtc,
                    item.Executions.Count))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "listar los lotes");

            return Array.Empty<BatchSummary>();
        }
    }

    /// <inheritdoc />
    public async Task<RecoverableExecution?> FindResumableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // §5 del diseño de datos: el último intento en uno de los tres estados recuperables. Un lote
            // cerrado (Anexo F.8) no se reanuda nunca, y de un lote sólo cuenta su ÚLTIMA ejecución: una
            // ejecución anterior ya reemplazada no se ofrece, aunque su estado haya quedado recuperable.
            var candidate = await context.BatchExecutions
                .AsNoTracking()
                .Where(execution =>
                    ((execution.Status == BatchStatus.EJECUTANDO
                        || execution.Status == BatchStatus.PAUSADO_POR_INCONSISTENCIA
                        || execution.Status == BatchStatus.LISTO_PARA_REANUDAR)
                        && execution.Batch != null
                        && execution.Batch.Status != BatchStatus.COMPLETADO
                        && execution.Batch.Status != BatchStatus.CANCELADO)
                    && !context.BatchExecutions.Any(
                        later => later.BatchId == execution.BatchId && later.Number > execution.Number))
                .OrderByDescending(execution => execution.StartedAtUtc)
                .ThenByDescending(execution => execution.Id)
                .Select(execution => new
                {
                    execution.Id,
                    execution.BatchId,
                    execution.Number,
                    execution.Status,
                    Code = execution.Batch != null ? execution.Batch.Code : string.Empty,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return candidate is null
                ? null
                : new RecoverableExecution(
                    candidate.Id,
                    candidate.BatchId,
                    candidate.Code,
                    candidate.Number,
                    candidate.Status);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "buscar la ejecución interrumpida");

            return null;
        }
    }

    /// <summary>Lote con sus ejecuciones y las fases de cada ejecución, sin arrastrar cambios al contexto.</summary>
    private static IQueryable<Batch> WithHistory(IQueryable<Batch> batches) =>
        batches
            .AsNoTracking()
            .Include(batch => batch.Executions)
                .ThenInclude(execution => execution.Phases);

    private static BatchStateSnapshot ToSnapshot(Batch batch) =>
        new(
            batch.Id,
            batch.Code,
            batch.PeriodYear,
            batch.PeriodMonth,
            batch.Status,
            batch.CreatedAtUtc,
            batch.CreatedBy,
            batch.Executions
                .OrderBy(execution => execution.Number)
                .Select(execution => new ExecutionSnapshot(
                    execution.Id,
                    execution.Number,
                    execution.Status,
                    execution.StartedAtUtc,
                    execution.FinishedAtUtc,
                    execution.LastCheckpointAtUtc,
                    execution.ResumedFromExecutionId,
                    execution.Phases
                        .OrderBy(phase => phase.Phase)
                        .Select(phase => new PhaseSnapshot(
                            phase.Phase,
                            phase.Status,
                            phase.ProcessedCount,
                            phase.TotalCount))
                        .ToArray()))
                .ToArray());

    private void LogStorageFailure(Exception exception, string action) =>
        _logger.LogError(
            exception,
            "No se pudo {Action}: la base local no está accesible o no está migrada.",
            action);
}
