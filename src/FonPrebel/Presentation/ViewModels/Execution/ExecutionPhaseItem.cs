using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Presentation.ViewModels.Execution;

/// <summary>
/// Una de las cinco fases tal como la pinta la pantalla Ejecución (ficha V1, §2.18): el número, el nombre,
/// el punto de estado, el porcentaje y el contador "procesados de total".
/// </summary>
/// <remarks>
/// Es inmutable y se arma en cada refresco desde <see cref="PhaseSnapshot"/>: la cifra siempre viene de
/// <c>PhaseExecution.ProcessedCount</c> y <c>TotalCount</c> persistidos, nunca de un contador en memoria.
/// Cuando <c>TotalCount</c> es nulo el porcentaje es "—" y la barra no se pinta: no se muestra un 0 % que
/// el dato no respalda.
/// </remarks>
public sealed record ExecutionPhaseItem(
    int Number,
    string NumberText,
    string Name,
    string StatusLabel,
    string StatusName,
    string StatusColor,
    string DotFill,
    string DotStroke,
    string NumberForeground,
    bool ShowsSpinner,
    bool HasKnownTotal,
    double ProgressValue,
    string PercentageText,
    string CountText)
{
    /// <summary>Rótulo de la barra: "Fase 1 · Consolidación".</summary>
    public string Label => $"Fase {Number} · {Name}";

    /// <summary>El número se oculta cuando el punto muestra el anillo de actividad.</summary>
    public bool ShowsNumber => !ShowsSpinner;

    /// <summary>Ítem de una fase cuando no hay ninguna ejecución: se lee "Sin lote" y el punto va vacío.</summary>
    public static ExecutionPhaseItem WithoutBatch(int number) =>
        new(
            number,
            number.ToString(),
            ExecutionLabels.PhaseName(number),
            ExecutionLabels.NoBatchStatus,
            ExecutionLabels.NoBatchStatus,
            ExecutionLabels.PendingColor,
            "Transparent",
            "#B4B4B4",
            "#7A7A7A",
            ShowsSpinner: false,
            HasKnownTotal: false,
            ProgressValue: 0,
            PercentageText: "—",
            CountText: "—");

    /// <summary>Ítem de una fase dentro de una ejecución. Sin fila persistida se lee como pendiente.</summary>
    public static ExecutionPhaseItem ForPhase(int number, PhaseSnapshot? snapshot)
    {
        PhaseStatus status = snapshot?.Status ?? PhaseStatus.PENDIENTE;
        int processed = snapshot?.ProcessedCount ?? 0;
        int? total = snapshot?.TotalCount;

        bool hasKnownTotal = total is > 0;
        double progress = hasKnownTotal ? Math.Clamp(processed * 100.0 / total!.Value, 0, 100) : 0;
        string percentage = hasKnownTotal ? ExecutionLabels.Percentage(progress) : "—";
        string count = hasKnownTotal
            ? $"{ExecutionLabels.Count(processed)} de {ExecutionLabels.Count(total!.Value)}"
            : $"{ExecutionLabels.Count(processed)} procesados · total pendiente";

        return new ExecutionPhaseItem(
            number,
            number.ToString(),
            ExecutionLabels.PhaseName(number),
            ExecutionLabels.PhaseStatusLabel(status),
            status.ToString(),
            ExecutionLabels.PhaseStatusColor(status),
            FillFor(status),
            StrokeFor(status),
            ForegroundFor(status),
            ShowsSpinner: status is PhaseStatus.EN_EJECUCION or PhaseStatus.ESPERANDO_CONFIRMACION,
            hasKnownTotal,
            progress,
            percentage,
            count);
    }

    /// <summary>Completada, fallida y cancelada van con el punto lleno; el resto, con el borde pintado.</summary>
    private static string FillFor(PhaseStatus status) => status switch
    {
        PhaseStatus.COMPLETADA => ExecutionLabels.CompletedColor,
        PhaseStatus.FALLIDA => ExecutionLabels.FailedColor,
        PhaseStatus.CANCELADA => ExecutionLabels.CancelledColor,
        _ => "Transparent",
    };

    private static string StrokeFor(PhaseStatus status) => status switch
    {
        PhaseStatus.COMPLETADA => ExecutionLabels.CompletedColor,
        PhaseStatus.FALLIDA => ExecutionLabels.FailedColor,
        PhaseStatus.CANCELADA => ExecutionLabels.CancelledColor,
        PhaseStatus.EN_EJECUCION => ExecutionLabels.RunningColor,
        PhaseStatus.ESPERANDO_CONFIRMACION => ExecutionLabels.WaitingColor,
        _ => "#B4B4B4",
    };

    /// <summary>El número se lee en blanco sobre el punto lleno y con el color del estado sobre el vacío.</summary>
    private static string ForegroundFor(PhaseStatus status) => status switch
    {
        PhaseStatus.COMPLETADA or PhaseStatus.FALLIDA or PhaseStatus.CANCELADA => "White",
        PhaseStatus.EN_EJECUCION => ExecutionLabels.RunningColor,
        PhaseStatus.ESPERANDO_CONFIRMACION => ExecutionLabels.WaitingColor,
        _ => "#7A7A7A",
    };
}
