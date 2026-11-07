using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Persistencia de las huellas de los archivos de entrada de una ejecución (§3.7 del diseño de datos).
/// Registrar una huella del mismo papel reemplaza a la anterior: repetir la captura no duplica filas
/// (Anexo F.9).
/// </summary>
public sealed class InputFingerprintStore : IInputFingerprintStore
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<InputFingerprintStore> _logger;

    public InputFingerprintStore(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<InputFingerprintStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> SaveAsync(
        int executionId,
        IReadOnlyList<ExecutionInputFingerprint> fingerprints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            bool executionExists = await context.BatchExecutions
                .AsNoTracking()
                .AnyAsync(execution => execution.Id == executionId, cancellationToken)
                .ConfigureAwait(false);

            if (!executionExists)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.ExecutionNotFound,
                    $"No existe la ejecución {executionId}: no se registraron huellas.");
            }

            List<ExecutionInputFingerprint> stored = await context.ExecutionInputFingerprints
                .Where(fingerprint => fingerprint.BatchExecutionId == executionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (ExecutionInputFingerprint fingerprint in fingerprints)
            {
                fingerprint.BatchExecutionId = executionId;

                ExecutionInputFingerprint? row = stored.FirstOrDefault(
                    item => string.Equals(item.Role, fingerprint.Role, StringComparison.Ordinal));

                if (row is null)
                {
                    context.ExecutionInputFingerprints.Add(fingerprint);

                    continue;
                }

                row.FilePath = fingerprint.FilePath;
                row.FileSizeBytes = fingerprint.FileSizeBytes;
                row.FileModifiedAtUtc = fingerprint.FileModifiedAtUtc;
                row.Sha256 = fingerprint.Sha256;
                row.CapturedAtUtc = fingerprint.CapturedAtUtc;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.SuccessWith(
                $"{fingerprints.Count} huellas registradas en la ejecución {executionId}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "No se pudieron guardar las huellas de la ejecución {Ejecucion}: la base local no está accesible o no está migrada. No se modificó nada.",
                executionId);

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<ExecutionInputFingerprint>>> ListAsync(
        int executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            List<ExecutionInputFingerprint> fingerprints = await context.ExecutionInputFingerprints
                .AsNoTracking()
                .Where(fingerprint => fingerprint.BatchExecutionId == executionId)
                .OrderBy(fingerprint => fingerprint.Role)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return BatchOperationResult<IReadOnlyList<ExecutionInputFingerprint>>.Ok(fingerprints);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "No se pudieron leer las huellas de la ejecución {Ejecucion}: la base local no está accesible o no está migrada.",
                executionId);

            return BatchOperationResult<IReadOnlyList<ExecutionInputFingerprint>>.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada.");
        }
    }
}
