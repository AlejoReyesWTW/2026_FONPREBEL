namespace FonPrebel.Domain.Enums;

/// <summary>
/// Estado del PDF de un jubilado dentro del lote (§4.3). Un PDF confirmado nunca se regenera en una
/// reanudación (§2.16).
/// </summary>
public enum PdfStatus
{
    /// <summary>El PDF todavía no se generó.</summary>
    PENDIENTE,

    /// <summary>El PDF existe con su cifrado aplicado.</summary>
    GENERADO,

    /// <summary>La generación del PDF falló.</summary>
    FALLIDO,
}
