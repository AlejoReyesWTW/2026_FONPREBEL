namespace FonPrebel.Domain.Enums;

/// <summary>
/// Estado de una fase dentro de una ejecución (§4.2). Los nombres son los del diseño y quedan en
/// español porque son estados visibles para el analista.
/// </summary>
public enum PhaseStatus
{
    /// <summary>La fase todavía no empezó.</summary>
    PENDIENTE,

    /// <summary>La fase está procesando.</summary>
    EN_EJECUCION,

    /// <summary>La fase terminó su trabajo y espera la confirmación del analista.</summary>
    ESPERANDO_CONFIRMACION,

    /// <summary>La fase terminó y fue confirmada.</summary>
    COMPLETADA,

    /// <summary>La fase terminó con error.</summary>
    FALLIDA,

    /// <summary>La fase se canceló.</summary>
    CANCELADA,
}
