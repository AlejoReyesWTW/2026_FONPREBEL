using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Enums;
using FonPrebel.Presentation.ViewModels.Execution;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// Las cinco fases de la pantalla Ejecución (ficha V1, §2.18): el porcentaje y el contador salen del dato
/// persistido, y cuando el total no está se lee "—" en lugar de un cero que el dato no respalda.
/// </summary>
public sealed class ExecutionPhaseItemTests
{
    [Fact]
    public void Without_a_batch_every_phase_reads_as_empty_and_shows_no_percentage()
    {
        ExecutionPhaseItem item = ExecutionPhaseItem.WithoutBatch(2);

        Assert.Contains("Fase 2", item.Label, StringComparison.Ordinal);
        Assert.False(item.HasKnownTotal);
        Assert.False(item.ShowsSpinner);
        Assert.Equal("—", item.PercentageText);
        Assert.Equal("—", item.CountText);
        Assert.Equal(0d, item.ProgressValue);
    }

    [Fact]
    public void A_phase_without_a_persisted_total_shows_what_was_processed_and_a_pending_total()
    {
        ExecutionPhaseItem item = ExecutionPhaseItem.ForPhase(
            3,
            new PhaseSnapshot(3, PhaseStatus.COMPLETADA, ProcessedCount: 7, TotalCount: null));

        Assert.False(item.HasKnownTotal);
        Assert.Equal("—", item.PercentageText);
        Assert.Equal(0d, item.ProgressValue);
        Assert.Contains("7 procesados", item.CountText, StringComparison.Ordinal);
        Assert.Contains("total pendiente", item.CountText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_phase_with_a_total_reports_its_percentage_and_count_from_the_persisted_rows()
    {
        ExecutionPhaseItem item = ExecutionPhaseItem.ForPhase(
            1,
            new PhaseSnapshot(1, PhaseStatus.COMPLETADA, ProcessedCount: 10, TotalCount: 10));

        Assert.True(item.HasKnownTotal);
        Assert.Equal(100d, item.ProgressValue);
        Assert.Contains("100", item.PercentageText, StringComparison.Ordinal);
    }

    [Fact]
    public void Progress_never_exceeds_one_hundred_percent()
    {
        ExecutionPhaseItem item = ExecutionPhaseItem.ForPhase(
            1,
            new PhaseSnapshot(1, PhaseStatus.EN_EJECUCION, ProcessedCount: 150, TotalCount: 100));

        Assert.True(item.HasKnownTotal);
        Assert.Equal(100d, item.ProgressValue);
        Assert.Contains("150", item.CountText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_running_phase_shows_the_activity_ring_and_hides_the_number()
    {
        ExecutionPhaseItem running = ExecutionPhaseItem.ForPhase(
            1,
            new PhaseSnapshot(1, PhaseStatus.EN_EJECUCION, ProcessedCount: 0, TotalCount: 10));
        ExecutionPhaseItem finished = ExecutionPhaseItem.ForPhase(
            1,
            new PhaseSnapshot(1, PhaseStatus.COMPLETADA, ProcessedCount: 10, TotalCount: 10));

        Assert.True(running.ShowsSpinner);
        Assert.False(running.ShowsNumber);
        Assert.False(finished.ShowsSpinner);
        Assert.True(finished.ShowsNumber);
    }
}
