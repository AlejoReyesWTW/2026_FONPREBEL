using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Domain.ValueObjects;

/// <summary>
/// Las cinco fases del proceso mensual (§2.8, tabla <c>PhaseExecution</c> del §3.3 del diseño de datos).
/// Es el único lugar que fija cuántas son y con qué estado nacen.
/// </summary>
public static class PhaseCatalog
{
    /// <summary>Cantidad de fases de cada ejecución.</summary>
    public const int Count = 5;

    /// <summary>Números de fase, de 1 a 5.</summary>
    public static IReadOnlyList<int> Numbers { get; } = Enumerable.Range(1, Count).ToArray();

    /// <summary>Filas de fase de una ejecución nueva, todas en <see cref="PhaseStatus.PENDIENTE"/>.</summary>
    public static List<PhaseExecution> CreatePending() =>
        Numbers
            .Select(number => new PhaseExecution { Phase = number, Status = PhaseStatus.PENDIENTE })
            .ToList();
}
