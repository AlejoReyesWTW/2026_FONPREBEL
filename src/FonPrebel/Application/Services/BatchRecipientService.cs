using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// S8 (§3.4 del diseño de datos): destinatarios del lote con la cédula normalizada y la clave única
/// <c>(BatchId, Cedula)</c>, y consulta de las reglas de no duplicación.
/// </summary>
/// <remarks>
/// La cédula se normaliza en un solo lugar (<see cref="Cedula"/>), así que <c>8.123.456</c> y <c>8123456</c>
/// son el mismo jubilado y el índice único del esquema hace cumplir la regla. Las listas de "reenviables",
/// "confirmados como enviados" y "PDF regenerables" aplican
/// <see cref="RecipientProcessingPolicy"/>, que es la única dueña de esas reglas.
/// </remarks>
public sealed class BatchRecipientService : IBatchRecipientService
{
    private readonly IBatchRecipientStore _store;
    private readonly RecipientProcessingPolicy _policy;
    private readonly ILogger<BatchRecipientService> _logger;

    public BatchRecipientService(
        IBatchRecipientStore store,
        RecipientProcessingPolicy policy,
        ILogger<BatchRecipientService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _policy = policy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> RegisterAsync(
        int batchId,
        IReadOnlyList<RecipientRegistration> recipients,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipients);

        if (recipients.Count == 0)
        {
            return BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "El lote no trae destinatarios que registrar.");
        }

        DateTime now = DateTime.UtcNow;
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<BatchRecipient> rows = new(recipients.Count);

        foreach (RecipientRegistration registration in recipients)
        {
            ArgumentNullException.ThrowIfNull(registration);

            string cedula = Cedula.Normalize(registration.Cedula);

            if (cedula.Length == 0)
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.InvalidData,
                    "Hay un jubilado sin cédula: no se registra ninguno del lote.");
            }

            if (!seen.Add(cedula))
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.DuplicateRecipient,
                    $"La cédula {cedula} viene repetida en el mismo lote: la clave (lote, cédula) es única.");
            }

            rows.Add(new BatchRecipient
            {
                BatchId = batchId,
                Cedula = cedula,
                FullName = Normalize(registration.FullName),
                Email = Normalize(registration.Email),
                PdfStatus = PdfStatus.PENDIENTE,
                SendStatus = SendStatus.PENDIENTE,
                UpdatedAtUtc = now,
            });
        }

        BatchOperationResult stored = await _store.AddRangeAsync(batchId, rows, cancellationToken).ConfigureAwait(false);

        if (stored.Succeeded)
        {
            _logger.LogInformation(
                "Lote {Lote}: {Cantidad} destinatarios registrados",
                batchId,
                rows.Count);
        }
        else
        {
            _logger.LogWarning(
                "Lote {Lote}: no se registraron destinatarios. {Motivo}: {Detalle}",
                batchId,
                stored.Outcome,
                stored.Detail);
        }

        return stored;
    }

    /// <inheritdoc />
    public Task<BatchOperationResult> UpdatePdfStatusAsync(
        RecipientPdfUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        string cedula = Cedula.Normalize(update.Cedula);

        if (cedula.Length == 0 || !Enum.IsDefined(update.Status))
        {
            return Task.FromResult(BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "La actualización del PDF necesita una cédula y un estado del diseño."));
        }

        return _store.UpdatePdfAsync(
            update.BatchId,
            cedula,
            update.Status,
            Normalize(update.PdfPath),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<BatchOperationResult> UpdateSendStatusAsync(
        RecipientSendUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        string cedula = Cedula.Normalize(update.Cedula);

        if (cedula.Length == 0 || !Enum.IsDefined(update.Status))
        {
            return Task.FromResult(BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "La actualización del envío necesita una cédula y un estado del diseño."));
        }

        if (update.Status == SendStatus.ENVIADO && update.ConfirmedAtUtc is null)
        {
            // §2.8 regla 12: la aplicación no afirma una entrega que no confirmó el mecanismo de envío.
            return Task.FromResult(BatchOperationResult.Failed(
                BatchOutcome.ConfirmationRequired,
                "No se marca un correo como enviado sin la confirmación del mecanismo de envío (§2.8 regla 12)."));
        }

        return _store.UpdateSendAsync(
            update.BatchId,
            cedula,
            update.Status,
            update.ConfirmedAtUtc,
            Normalize(update.ErrorCode),
            Normalize(update.ErrorMessage),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListAsync(
        int batchId,
        CancellationToken cancellationToken = default) =>
        _store.ListAsync(batchId, cancellationToken);

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListResendableAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        BatchOperationResult<IReadOnlyList<BatchRecipient>> listed =
            await _store.ListAsync(batchId, cancellationToken).ConfigureAwait(false);

        if (!listed.Succeeded)
        {
            return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Failed(listed.Outcome, listed.Detail);
        }

        IReadOnlyList<BatchRecipient> resendable = listed.Value!
            .Where(recipient => _policy.CanResend(recipient.SendStatus))
            .ToArray();

        return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Ok(resendable);
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListConfirmedSentAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        BatchOperationResult<IReadOnlyList<BatchRecipient>> listed =
            await _store.ListAsync(batchId, cancellationToken).ConfigureAwait(false);

        if (!listed.Succeeded)
        {
            return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Failed(listed.Outcome, listed.Detail);
        }

        IReadOnlyList<BatchRecipient> sent = listed.Value!
            .Where(recipient => _policy.IsReportedAsSent(recipient.SendStatus))
            .ToArray();

        return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Ok(sent);
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListRegenerablePdfAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        BatchOperationResult<IReadOnlyList<BatchRecipient>> listed =
            await _store.ListAsync(batchId, cancellationToken).ConfigureAwait(false);

        if (!listed.Succeeded)
        {
            return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Failed(listed.Outcome, listed.Detail);
        }

        IReadOnlyList<BatchRecipient> regenerable = listed.Value!
            .Where(recipient => _policy.CanRegeneratePdf(recipient.PdfStatus))
            .ToArray();

        return BatchOperationResult<IReadOnlyList<BatchRecipient>>.Ok(regenerable);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
