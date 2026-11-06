namespace FonPrebel.Domain.Enums;

/// <summary>
/// Estado del lote y de cada ejecución (§4.1). Los nombres son literales del Anexo F.3 del spec, por
/// eso quedan en español: son estados que el analista ve en la interfaz.
/// </summary>
public enum BatchStatus
{
    /// <summary>El lote existe y no tiene trabajo en curso.</summary>
    LISTO,

    /// <summary>Una ejecución del lote está en curso.</summary>
    EJECUTANDO,

    /// <summary>Se detectó una inconsistencia en las entradas y el proceso espera al analista.</summary>
    PAUSADO_POR_INCONSISTENCIA,

    /// <summary>Se están recalculando las huellas de los archivos de entrada (Anexo F).</summary>
    VALIDANDO_RECUPERACION,

    /// <summary>Las entradas coinciden y el lote puede reanudarse.</summary>
    LISTO_PARA_REANUDAR,

    /// <summary>Las cinco fases terminaron.</summary>
    COMPLETADO,

    /// <summary>El analista canceló el lote.</summary>
    CANCELADO,
}
