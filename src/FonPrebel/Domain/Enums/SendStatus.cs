namespace FonPrebel.Domain.Enums;

/// <summary>
/// Estado del envío del correo a un jubilado (§4.4). <see cref="INCIERTO"/> representa un resultado
/// desconocido y nunca se muestra como enviado (§2.8 regla 12).
/// </summary>
public enum SendStatus
{
    /// <summary>El correo todavía no se intentó enviar.</summary>
    PENDIENTE,

    /// <summary>El envío está en curso; un corte en este estado no se confunde con "no se intentó".</summary>
    ENVIANDO,

    /// <summary>El mecanismo de envío confirmó la entrega.</summary>
    ENVIADO,

    /// <summary>El envío falló con un error conocido.</summary>
    FALLIDO,

    /// <summary>Se perdió la conexión y no hay confirmación: nunca se reporta como enviado.</summary>
    INCIERTO,
}
