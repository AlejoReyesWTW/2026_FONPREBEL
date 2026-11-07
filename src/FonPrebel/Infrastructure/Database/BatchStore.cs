using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Persistencia de lotes y ejecuciones (§3.1 y §3.2 del diseño de datos). Cada operación abre su propio
/// contexto desde la factoría (S3) y las transiciones que tocan el lote y su ejecución se resuelven en un
/// único <c>SaveChanges</c>, dentro de una transacción corta.
/// </summary>
public sealed class BatchStore : IBatchStore
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<BatchStore> _logger;

    public BatchStore(IDbContextFactory<AppDbContext> contextFactory, ILogger<BatchStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListBatchCodesAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            return await context.Batches
                .AsNoTracking()
                .Where(batch => batch.PeriodYear == periodYear && batch.PeriodMonth == periodMonth)
                .Select(batch => batch.Code)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "leer los identificadores de lote del periodo");

            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<BatchCreated>> AddBatchAsync(
        Batch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            context.Batches.Add(batch);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            BatchExecution execution = batch.Executions.OrderBy(item => item.Number).First();

            return BatchOperationResult<BatchCreated>.Ok(
                new BatchCreated(batch.Id, batch.Code, execution.Id, execution.Number));
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsUniqueViolation(exception))
        {
            _logger.LogWarning(
                "El identificador de lote {Codigo} ya está usado: se genera otro en su lugar",
                batch.Code);

            return BatchOperationResult<BatchCreated>.Failed(
                BatchOutcome.DuplicateCode,
                $"El identificador de lote {batch.Code} ya está usado.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "guardar el lote");

            return BatchOperationResult<BatchCreated>.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se guardó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<ExecutionAppended>> StartExecutionAsync(
        int executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            BatchExecution? execution = await context.BatchExecutions
                .Include(item => item.Batch)
                .SingleOrDefaultAsync(item => item.Id == executionId, cancellationToken)
                .ConfigureAwait(false);

            if (execution is null)
            {
                return BatchOperationResult<ExecutionAppended>.Failed(
                    BatchOutcome.ExecutionNotFound,
                    $"No existe la ejecución {executionId}.");
            }

            if (execution.Status != BatchStatus.LISTO)
            {
                return BatchOperationResult<ExecutionAppended>.Failed(
                    BatchOutcome.InvalidExecutionState,
                    $"La ejecución {executionId} está en estado {execution.Status} y sólo se inicia desde LISTO.");
            }

            execution.Status = BatchStatus.EJECUTANDO;

            if (execution.Batch is not null)
            {
                execution.Batch.Status = BatchStatus.EJECUTANDO;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult<ExecutionAppended>.Ok(
                new ExecutionAppended(execution.BatchId, execution.Id, execution.Number, execution.ResumedFromExecutionId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "iniciar la ejecución");

            return BatchOperationResult<ExecutionAppended>.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<ExecutionAppended>> AddResumeExecutionAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            Batch? batch = await context.Batches
                .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
                .ConfigureAwait(false);

            if (batch is null)
            {
                return BatchOperationResult<ExecutionAppended>.Failed(
                    BatchOutcome.BatchNotFound,
                    $"No existe el lote {batchId}.");
            }

            BatchExecution? previous = await context.BatchExecutions
                .Where(item => item.BatchId == batchId)
                .OrderByDescending(item => item.Number)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            // Anexo F.6 y F.9: lo válido se conserva, así que la ejecución nueva hereda las huellas de los
            // insumos que se validaron, en lugar de quedarse sin identidad de entrada.
            List<ExecutionInputFingerprint> inherited = previous is null
                ? new List<ExecutionInputFingerprint>()
                : await context.ExecutionInputFingerprints
                    .AsNoTracking()
                    .Where(fingerprint => fingerprint.BatchExecutionId == previous.Id)
                    .Select(fingerprint => new ExecutionInputFingerprint
                    {
                        Role = fingerprint.Role,
                        FilePath = fingerprint.FilePath,
                        FileSizeBytes = fingerprint.FileSizeBytes,
                        FileModifiedAtUtc = fingerprint.FileModifiedAtUtc,
                        Sha256 = fingerprint.Sha256,
                        CapturedAtUtc = fingerprint.CapturedAtUtc,
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            BatchExecution execution = new()
            {
                BatchId = batchId,
                Number = (previous?.Number ?? 0) + 1,
                Status = BatchStatus.EJECUTANDO,
                StartedAtUtc = DateTime.UtcNow,
                ResumedFromExecutionId = previous?.Id,
                Phases = PhaseCatalog.CreatePending(),
            };

            // La ejecución de la que se reanuda deja de ser recuperable: se cierra en el mismo guardado, así
            // que no queda un estado recuperable eterno ni se ofrece otra vez (Anexo F.8). El lote no se
            // cierra: sigue EJECUTANDO con la ejecución nueva, que es la que continúa el trabajo.
            if (previous is not null)
            {
                previous.Status = BatchStatus.COMPLETADO;
                previous.FinishedAtUtc ??= execution.StartedAtUtc;
            }

            foreach (ExecutionInputFingerprint fingerprint in inherited)
            {
                execution.InputFingerprints.Add(fingerprint);
            }

            context.BatchExecutions.Add(execution);
            batch.Status = BatchStatus.EJECUTANDO;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult<ExecutionAppended>.Ok(
                new ExecutionAppended(batchId, execution.Id, execution.Number, execution.ResumedFromExecutionId));
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsUniqueViolation(exception))
        {
            _logger.LogWarning(
                "El lote {Lote} ya tenía la ejecución siguiente: no se duplica el número de ejecución",
                batchId);

            return BatchOperationResult<ExecutionAppended>.Failed(
                BatchOutcome.InvalidExecutionState,
                "El lote ya tiene la ejecución siguiente registrada: no se duplica.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "reanudar el lote");

            return BatchOperationResult<ExecutionAppended>.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> CloseExecutionAsync(
        int executionId,
        BatchStatus terminalStatus,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            BatchExecution? execution = await context.BatchExecutions
                .Include(item => item.Batch)
                .SingleOrDefaultAsync(item => item.Id == executionId, cancellationToken)
                .ConfigureAwait(false);

            if (execution is null)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.ExecutionNotFound,
                    $"No existe la ejecución {executionId}.");
            }

            if (execution.Status is BatchStatus.COMPLETADO or BatchStatus.CANCELADO)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.InvalidExecutionState,
                    $"La ejecución {executionId} ya está cerrada como {execution.Status}.");
            }

            execution.Status = terminalStatus;
            execution.FinishedAtUtc = DateTime.UtcNow;

            if (execution.Batch is not null)
            {
                execution.Batch.Status = terminalStatus;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "cerrar la ejecución");

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> SetStateAsync(
        int executionId,
        BatchStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            BatchExecution? execution = await context.BatchExecutions
                .Include(item => item.Batch)
                .SingleOrDefaultAsync(item => item.Id == executionId, cancellationToken)
                .ConfigureAwait(false);

            if (execution is null)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.ExecutionNotFound,
                    $"No existe la ejecución {executionId}.");
            }

            execution.Status = status;

            if (execution.Batch is not null)
            {
                execution.Batch.Status = status;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "cambiar el estado de la ejecución");

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    private void LogStorageFailure(Exception exception, string action) =>
        _logger.LogError(
            exception,
            "No se pudo {Action}: la base local no está accesible o no está migrada. No se modificó nada.",
            action);
}

/// <summary>
/// Reconoce la violación de un índice único de SQLite a partir de la excepción de EF: la clave duplicada se
/// informa como resultado tipado y no como error inesperado. Se usan los códigos extendidos de
/// <c>SQLITE_CONSTRAINT</c> y no el genérico, para no confundirla con otra restricción.
/// </summary>
internal static class UniqueConstraintViolation
{
    private const int UniqueExtendedCode = 2067;
    private const int PrimaryKeyExtendedCode = 1555;

    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is SqliteException sqlite
            && sqlite.SqliteExtendedErrorCode is UniqueExtendedCode or PrimaryKeyExtendedCode;
    }
}
