using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Destinatarios del lote: registro con la cédula normalizada y clave única <c>(BatchId, Cedula)</c>, y
/// actualización del resultado por jubilado (§3.4 del diseño de datos).
/// </summary>
/// <remarks>
/// Las consultas de "reenviables", "confirmados como enviados" y "PDF regenerables" aplican las reglas de
/// no duplicación en un solo lugar (<see cref="Services.RecipientProcessingPolicy"/>): el envío y la
/// generación son de otra unidad, acá sólo se responde qué corresponde hacer.
/// </remarks>
public interface IBatchRecipientService
{
    /// <summary>
    /// Registra jubilados en un lote normalizando la cédula. Es todo o nada: si alguna cédula ya está en el
    /// lote o viene repetida en el pedido, no se guarda ninguna y se informa
    /// <see cref="BatchOutcome.DuplicateRecipient"/>.
    /// </summary>
    Task<BatchOperationResult> RegisterAsync(
        int batchId,
        IReadOnlyList<RecipientRegistration> recipients,
        CancellationToken cancellationToken = default);

    /// <summary>Registra o actualiza el estado del PDF de un jubilado del lote.</summary>
    Task<BatchOperationResult> UpdatePdfStatusAsync(
        RecipientPdfUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra o actualiza el estado de envío de un jubilado del lote. Marcar
    /// <see cref="SendStatus.ENVIADO"/> exige la confirmación del mecanismo de envío: sin ella no se escribe
    /// nada (§2.8 regla 12). Un envío ya confirmado no vuelve a <see cref="SendStatus.PENDIENTE"/>: se
    /// rechaza con <see cref="BatchOutcome.InvalidSendTransition"/> y el destinatario queda intacto (§2.8
    /// regla 6).
    /// </summary>
    Task<BatchOperationResult> UpdateSendStatusAsync(
        RecipientSendUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>Jubilados del lote, ordenados por cédula.</summary>
    Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>Jubilados a los que todavía corresponde enviarles el correo (§2.8 regla 6).</summary>
    Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListResendableAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>Jubilados con envío confirmado. Un envío incierto no aparece acá (§2.8 regla 12).</summary>
    Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListConfirmedSentAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>Jubilados cuyo PDF todavía corresponde generar (§2.16, Anexo F.9).</summary>
    Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListRegenerablePdfAsync(
        int batchId,
        CancellationToken cancellationToken = default);
}

/// <summary>Datos de un jubilado que se registra en el lote. La cédula se normaliza antes de guardarla.</summary>
public sealed record RecipientRegistration(string Cedula, string? FullName = null, string? Email = null);

/// <summary>Actualización del resultado del PDF de un jubilado del lote.</summary>
public sealed record RecipientPdfUpdate(int BatchId, string Cedula, PdfStatus Status, string? PdfPath = null);

/// <summary>
/// Actualización del resultado de envío de un jubilado del lote. <paramref name="ConfirmedAtUtc"/> sólo se
/// escribe con <see cref="SendStatus.ENVIADO"/> y es obligatorio en ese caso (§2.8 regla 12).
/// </summary>
public sealed record RecipientSendUpdate(
    int BatchId,
    string Cedula,
    SendStatus Status,
    DateTime? ConfirmedAtUtc = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>Persistencia de los destinatarios de un lote (§3.4).</summary>
public interface IBatchRecipientStore
{
    /// <summary>
    /// Inserta los jubilados en una sola transacción. La cédula llega ya normalizada; una clave
    /// <c>(BatchId, Cedula)</c> repetida se informa como <see cref="BatchOutcome.DuplicateRecipient"/> y no
    /// se guarda nada.
    /// </summary>
    Task<BatchOperationResult> AddRangeAsync(
        int batchId,
        IReadOnlyList<BatchRecipient> recipients,
        CancellationToken cancellationToken = default);

    /// <summary>Jubilados del lote, ordenados por cédula, sin quedar enganchados al contexto.</summary>
    Task<BatchOperationResult<IReadOnlyList<BatchRecipient>>> ListAsync(
        int batchId,
        CancellationToken cancellationToken = default);

    /// <summary>Escribe el estado del PDF de un jubilado del lote. Una ruta nula deja intacta la guardada.</summary>
    Task<BatchOperationResult> UpdatePdfAsync(
        int batchId,
        string cedula,
        PdfStatus status,
        string? pdfPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escribe el estado de envío de un jubilado del lote. Un envío confirmado no se puede retroceder a
    /// <see cref="SendStatus.PENDIENTE"/>: se informa <see cref="BatchOutcome.InvalidSendTransition"/> y no se
    /// modifica la fila (§2.8 reglas 6 y 12).
    /// </summary>
    Task<BatchOperationResult> UpdateSendAsync(
        int batchId,
        string cedula,
        SendStatus status,
        DateTime? confirmedAtUtc,
        string? errorCode,
        string? errorMessage,
        CancellationToken cancellationToken = default);
}
