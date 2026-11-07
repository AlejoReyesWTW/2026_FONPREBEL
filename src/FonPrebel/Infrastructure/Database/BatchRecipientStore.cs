using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Persistencia de los destinatarios de un lote (§3.4 del diseño de datos), con la clave única
/// <c>(BatchId, Cedula)</c> que el esquema hace cumplir. Un contexto por operación.
/// </summary>
public sealed class BatchRecipientStore : IBatchRecipientStore
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly RecipientProcessingPolicy _policy;
    private readonly ILogger<BatchRecipientStore> _logger;

    public BatchRecipientStore(
        IDbContextFactory<AppDbContext> contextFactory,
        RecipientProcessingPolicy policy,
        ILogger<BatchRecipientStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _policy = policy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> AddRangeAsync(
        int batchId,
        IReadOnlyList<BatchRecipient> recipients,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipients);

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            bool batchExists = await context.Batches
                .AsNoTracking()
                .AnyAsync(batch => batch.Id == batchId, cancellationToken)
                .ConfigureAwait(false);

            if (!batchExists)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.BatchNotFound,
                    $"No existe el lote {batchId}: no se registraron destinatarios.");
            }

            List<string> cedulas = recipients.Select(recipient => recipient.Cedula).ToList();

            if (cedulas.Count != cedulas.Distinct(StringComparer.Ordinal).Count())
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.DuplicateRecipient,
                    "La misma cédula viene repetida en el pedido: la clave (lote, cédula) es única.");
            }

            List<string> alreadyStored = await context.BatchRecipients
                .AsNoTracking()
                .Where(recipient => recipient.BatchId == batchId && cedulas.Contains(recipient.Cedula))
                .Select(recipient => recipient.Cedula)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (alreadyStored.Count > 0)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.DuplicateRecipient,
                    $"La cédula {alreadyStored[0]} ya está registrada en el lote {batchId}.");
            }

            context.BatchRecipients.AddRange(recipients);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.SuccessWith($"{recipients.Count} destinatarios registrados en el lote {batchId}.");
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsUniqueViolation(exception))
        {
            return BatchOperationResult.Failed(
                BatchOutcome.DuplicateRecipient,
                $"Una cédula del pedido ya está registrada en el lote {batchId}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "registrar los destinatarios del lote");

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se guardó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            List<BatchRecipient> recipients = await context.BatchRecipients
                .AsNoTracking()
                .Where(recipient => recipient.BatchId == batchId)
                .OrderBy(recipient => recipient.Cedula)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Ok(recipients);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "leer los destinatarios del lote");

            return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> UpdatePdfAsync(
        int batchId,
        string cedula,
        PdfStatus status,
        string? pdfPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            BatchRecipient? recipient = await context.BatchRecipients
                .SingleOrDefaultAsync(
                    item => item.BatchId == batchId && item.Cedula == cedula,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipient is null)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.RecipientNotFound,
                    $"La cédula {cedula} no está registrada en el lote {batchId}.");
            }

            recipient.PdfStatus = status;

            // Una ruta nula no borra la guardada: no se descarta la evidencia de un PDF ya generado.
            if (pdfPath is not null)
            {
                recipient.PdfPath = pdfPath;
            }

            recipient.UpdatedAtUtc = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "actualizar el estado del PDF del destinatario");

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> UpdateSendAsync(
        int batchId,
        string cedula,
        SendStatus status,
        DateTime? confirmedAtUtc,
        string? errorCode,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            BatchRecipient? recipient = await context.BatchRecipients
                .SingleOrDefaultAsync(
                    item => item.BatchId == batchId && item.Cedula == cedula,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipient is null)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.RecipientNotFound,
                    $"La cédula {cedula} no está registrada en el lote {batchId}.");
            }

            // §2.8 reglas 6 y 12: un envío confirmado no vuelve a quedar pendiente de envío ni reenviable.
            if (!_policy.CanTransition(recipient.SendStatus, status))
            {
                _logger.LogWarning(
                    "Lote {Lote}: la cédula {Cedula} ya tiene el envío confirmado y no vuelve a {Estado}",
                    batchId,
                    cedula,
                    status);

                return BatchOperationResult.Failed(
                    BatchOutcome.InvalidSendTransition,
                    $"El correo de la cédula {cedula} ya fue confirmado como enviado: no vuelve a quedar " +
                    "pendiente de envío (§2.8 reglas 6 y 12).");
            }

            DateTime now = DateTime.UtcNow;

            recipient.SendStatus = status;
            recipient.LastErrorCode = errorCode;
            recipient.LastErrorMessage = errorMessage;
            recipient.UpdatedAtUtc = now;

            // Último intento sólo cuando hubo uno: un estado pendiente no cuenta como intento.
            if (status != SendStatus.PENDIENTE)
            {
                recipient.LastAttemptAtUtc = now;
            }

            // §2.8 regla 12: la confirmación sólo se escribe cuando el envío está confirmado como enviado.
            if (status == SendStatus.ENVIADO && confirmedAtUtc is not null)
            {
                recipient.ConfirmedAtUtc = confirmedAtUtc.Value.ToUniversalTime();
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BatchOperationResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "actualizar el estado de envío del destinatario");

            return BatchOperationResult.Failed(
                BatchOutcome.StorageUnavailable,
                "La base local no está accesible o no está migrada; no se modificó nada.");
        }
    }

    private void LogStorageFailure(Exception exception, string action) =>
        _logger.LogError(
            exception,
            "No se pudo {Action}: la base local no está accesible o no está migrada.",
            action);
}
