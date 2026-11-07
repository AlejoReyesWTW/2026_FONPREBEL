using FonPrebel.Domain.Enums;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Qué estados de lote y de ejecución se reanudan, en un solo lugar (§5 del diseño de datos y Anexo F.8).
/// Lo consultan la búsqueda de la ejecución recuperable, la reanudación y las pruebas de regresión.
/// </summary>
public static class BatchStatusRules
{
    /// <summary>
    /// Indica si el estado es terminal: el lote ya no tiene trabajo en curso y no se reanuda (Anexo F.8).
    /// </summary>
    public static bool IsTerminal(BatchStatus status) =>
        status is BatchStatus.COMPLETADO or BatchStatus.CANCELADO;

    /// <summary>
    /// Indica si una ejecución interrumpida todavía se puede reanudar (§5, paso 1):
    /// <see cref="BatchStatus.EJECUTANDO"/>, <see cref="BatchStatus.PAUSADO_POR_INCONSISTENCIA"/> o
    /// <see cref="BatchStatus.LISTO_PARA_REANUDAR"/>.
    /// </summary>
    public static bool IsRecoverable(BatchStatus status) =>
        status is BatchStatus.EJECUTANDO
            or BatchStatus.PAUSADO_POR_INCONSISTENCIA
            or BatchStatus.LISTO_PARA_REANUDAR;
}
