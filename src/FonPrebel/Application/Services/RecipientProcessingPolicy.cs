using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Services;

/// <summary>
/// S8 (§2.8 reglas 6 y 12, Anexo F.9): reglas de no duplicación de artefactos, en un solo lugar y
/// consultables. No envía correos ni genera PDFs: sólo responde qué corresponde hacer con cada estado.
/// </summary>
/// <remarks>
/// <para>
/// Un correo confirmado como enviado no se reenvía en una reanudación normal (§2.8 regla 6). Un PDF ya
/// generado no se regenera (§2.16, Anexo F.6).
/// </para>
/// <para>
/// Un envío <see cref="SendStatus.INCIERTO"/> no se informa como enviado (§2.8 regla 12) y tampoco entra en
/// "reenviables": la política de reintentos e inciertos sigue pendiente (§5.2), así que queda para revisión
/// en lugar de reprocesarse solo.
/// </para>
/// </remarks>
public sealed class RecipientProcessingPolicy
{
    /// <summary>Estados de envío a los que todavía corresponde enviarles el correo.</summary>
    public static IReadOnlyList<SendStatus> ResendableStatuses { get; } =
        new[] { SendStatus.PENDIENTE, SendStatus.FALLIDO };

    /// <summary>Estados del PDF a los que todavía corresponde generarles el archivo.</summary>
    public static IReadOnlyList<PdfStatus> RegenerableStatuses { get; } =
        new[] { PdfStatus.PENDIENTE, PdfStatus.FALLIDO };

    /// <summary>Indica si al jubilado todavía corresponde enviarle el correo.</summary>
    public bool CanResend(SendStatus status) => ResendableStatuses.Contains(status);

    /// <summary>Indica si al jubilado todavía corresponde generarle el PDF.</summary>
    public bool CanRegeneratePdf(PdfStatus status) => RegenerableStatuses.Contains(status);

    /// <summary>
    /// Indica si el estado de envío ya tiene la confirmación del mecanismo de envío (§2.8 regla 12).
    /// </summary>
    public bool IsConfirmed(SendStatus status) => status == SendStatus.ENVIADO;

    /// <summary>
    /// Indica si se puede escribir <paramref name="target"/> sobre <paramref name="current"/>. Un envío
    /// confirmado nunca vuelve a <see cref="SendStatus.PENDIENTE"/>: eso lo dejaría otra vez "pendiente de
    /// enviar" y reenviable, contra §2.8 reglas 6 y 12. Sólo se retrocede desde estados no confirmados; la
    /// política de reintentos sigue pendiente (§5.2).
    /// </summary>
    public bool CanTransition(SendStatus current, SendStatus target) =>
        target != SendStatus.PENDIENTE || !IsConfirmed(current);

    /// <summary>
    /// Indica si el envío puede informarse como enviado. Sólo la confirmación del mecanismo de envío lo
    /// permite; un resultado incierto no es una confirmación.
    /// </summary>
    public bool IsReportedAsSent(SendStatus status) => status == SendStatus.ENVIADO;
}
