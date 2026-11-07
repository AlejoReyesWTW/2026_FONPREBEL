using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Presentation.ViewModels.Metrics;

/// <summary>
/// Contadores del tablero de Métricas, calculados desde los registros persistidos en SQLite (§2.22):
/// nunca desde contadores propios en memoria.
/// </summary>
/// <remarks>
/// Los estados salen de las entidades reales: el lote y su ejecución de <see cref="BatchSummary"/>, el
/// PDF de <see cref="BatchRecipient.PdfStatus"/> y el envío de <see cref="BatchRecipient.SendStatus"/>.
/// Un envío pasa por <c>PENDIENTE</c>, <c>ENVIANDO</c>, <c>ENVIADO</c>, <c>FALLIDO</c> o <c>INCIERTO</c>, y
/// los cinco tienen su propio contador: <c>INCIERTO</c> nunca se suma como enviado (§2.8 regla 12).
/// </remarks>
public sealed record MetricsTotals(
    int BatchCount,
    int CompletedBatchCount,
    int RunningBatchCount,
    int InterruptedBatchCount,
    int CancelledBatchCount,
    int ExecutionCount,
    int RunningExecutionCount,
    int RecipientCount,
    int RecipientsWithEmail,
    int RecipientsWithoutEmail,
    int SendPending,
    int SendSending,
    int SendSent,
    int SendFailed,
    int SendUncertain,
    int PdfGenerated,
    int PdfPending,
    int PdfFailed,
    int FailedBatchReads)
{
    /// <summary>Todos los contadores en cero: lo que se muestra antes de la primera lectura.</summary>
    public static MetricsTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Verdadero cuando se pudieron leer los destinatarios de todos los lotes del alcance.</summary>
    public bool RecipientsComplete => FailedBatchReads == 0;

    /// <summary>
    /// Cuenta lotes y destinatarios del alcance pedido. <paramref name="failedBatchReads"/> informa los
    /// lotes cuyos destinatarios no se pudieron leer, para que la pantalla no presente una suma parcial
    /// como si fuera el total.
    /// </summary>
    public static MetricsTotals From(
        IReadOnlyList<BatchSummary> batches,
        IReadOnlyList<BatchRecipient> recipients,
        int failedBatchReads)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(recipients);

        int completedBatches = 0;
        int runningBatches = 0;
        int interruptedBatches = 0;
        int cancelledBatches = 0;
        int executions = 0;

        foreach (BatchSummary batch in batches)
        {
            executions += batch.ExecutionCount;

            switch (batch.Status)
            {
                case BatchStatus.COMPLETADO:
                    completedBatches++;
                    break;
                case BatchStatus.CANCELADO:
                    cancelledBatches++;
                    break;
                case BatchStatus.PAUSADO_POR_INCONSISTENCIA:
                case BatchStatus.VALIDANDO_RECUPERACION:
                case BatchStatus.LISTO_PARA_REANUDAR:
                    interruptedBatches++;
                    break;
                case BatchStatus.EJECUTANDO:
                    // Una ejecución en curso es la de un lote en EJECUTANDO: el estado del lote y el de
                    // su ejecución se escriben en la misma transacción (IBatchStore.SetStateAsync) y un
                    // lote tiene a lo sumo una ejecución en curso, así que no hace falta una consulta
                    // por lote para contarlas.
                    runningBatches++;
                    break;
            }
        }

        int withEmail = 0;
        int sendPending = 0;
        int sendSending = 0;
        int sendSent = 0;
        int sendFailed = 0;
        int sendUncertain = 0;
        int pdfGenerated = 0;
        int pdfPending = 0;
        int pdfFailed = 0;

        foreach (BatchRecipient recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Email))
            {
                withEmail++;
            }

            switch (recipient.SendStatus)
            {
                case SendStatus.ENVIANDO:
                    sendSending++;
                    break;
                case SendStatus.ENVIADO:
                    sendSent++;
                    break;
                case SendStatus.FALLIDO:
                    sendFailed++;
                    break;
                case SendStatus.INCIERTO:
                    sendUncertain++;
                    break;
                default:
                    sendPending++;
                    break;
            }

            switch (recipient.PdfStatus)
            {
                case PdfStatus.GENERADO:
                    pdfGenerated++;
                    break;
                case PdfStatus.FALLIDO:
                    pdfFailed++;
                    break;
                default:
                    pdfPending++;
                    break;
            }
        }

        return new MetricsTotals(
            batches.Count,
            completedBatches,
            runningBatches,
            interruptedBatches,
            cancelledBatches,
            executions,
            runningBatches,
            recipients.Count,
            withEmail,
            recipients.Count - withEmail,
            sendPending,
            sendSending,
            sendSent,
            sendFailed,
            sendUncertain,
            pdfGenerated,
            pdfPending,
            pdfFailed,
            failedBatchReads);
    }
}
