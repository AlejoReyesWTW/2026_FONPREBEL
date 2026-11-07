using System.IO;
using System.Linq;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// S8 (§2.8): el lote nace con su primera ejecución y las cinco fases en <c>PENDIENTE</c>, el identificador
/// es único por periodo y el inicio de la ejecución mueve el lote a <c>EJECUTANDO</c>.
/// </summary>
public sealed class BatchCreationTests
{
    [Fact]
    public async Task Create_registers_the_first_execution_with_the_five_phases_pending()
    {
        using BatchTestContext context = new();

        BatchCreated created = await context.CreateAsync();

        BatchStateSnapshot? batch = await context.State.GetBatchAsync(created.BatchId);

        Assert.NotNull(batch);
        Assert.Equal(created.Code, batch!.Code);
        Assert.Equal(2026, batch.PeriodYear);
        Assert.Equal(11, batch.PeriodMonth);
        Assert.Equal(BatchStatus.LISTO, batch.Status);
        Assert.False(string.IsNullOrWhiteSpace(batch.CreatedBy));

        ExecutionSnapshot execution = Assert.Single(batch.Executions);
        Assert.Equal(1, execution.Number);
        Assert.Equal(created.ExecutionId, execution.ExecutionId);
        Assert.Equal(BatchStatus.LISTO, execution.Status);
        Assert.Null(execution.ResumedFromExecutionId);
        Assert.Null(execution.FinishedAtUtc);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, execution.Phases.Select(phase => phase.Phase));
        Assert.All(execution.Phases, phase => Assert.Equal(PhaseStatus.PENDIENTE, phase.Status));
        Assert.All(execution.Phases, phase => Assert.Equal(0, phase.ProcessedCount));
    }

    [Fact]
    public async Task Create_generates_a_unique_code_for_the_same_period()
    {
        using BatchTestContext context = new();

        BatchCreated first = await context.CreateAsync();
        BatchCreated second = await context.CreateAsync();

        Assert.NotEqual(first.Code, second.Code);

        IReadOnlyList<BatchSummary> batches = await context.State.ListBatchesAsync();

        Assert.Equal(2, batches.Count);
        Assert.Equal(2, batches.Select(batch => batch.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(batches, batch => Assert.Equal(1, batch.ExecutionCount));
    }

    [Theory]
    [InlineData(2026, 13)]
    [InlineData(2026, 0)]
    [InlineData(0, 11)]
    public async Task Create_rejects_an_invalid_period(int periodYear, int periodMonth)
    {
        using BatchTestContext context = new();

        BatchOperationResult<BatchCreated> result = await context.Batches.CreateAsync(
            new BatchCreationRequest(periodYear, periodMonth, "analista"));

        Assert.Equal(BatchOutcome.InvalidPeriod, result.Outcome);
        Assert.Empty(await context.State.ListBatchesAsync());
    }

    [Fact]
    public async Task Create_rejects_a_batch_without_the_windows_user()
    {
        using BatchTestContext context = new();

        BatchOperationResult<BatchCreated> result = await context.Batches.CreateAsync(
            new BatchCreationRequest(2026, 11, "   "));

        Assert.Equal(BatchOutcome.InvalidData, result.Outcome);
        Assert.Empty(await context.State.ListBatchesAsync());
    }

    [Fact]
    public async Task Start_execution_moves_the_batch_to_running()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        BatchOperationResult<ExecutionAppended> started = await context.Batches.StartExecutionAsync(created.ExecutionId);

        Assert.True(started.Succeeded, started.Detail);
        Assert.Equal(1, started.Value!.ExecutionNumber);

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(BatchStatus.EJECUTANDO, batch.Status);
        Assert.Equal(BatchStatus.EJECUTANDO, Assert.Single(batch.Executions).Status);
    }

    [Fact]
    public async Task Start_execution_is_rejected_when_the_execution_is_not_ready()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        await context.Batches.StartExecutionAsync(created.ExecutionId);
        BatchOperationResult<ExecutionAppended> second = await context.Batches.StartExecutionAsync(created.ExecutionId);

        Assert.Equal(BatchOutcome.InvalidExecutionState, second.Outcome);

        BatchOperationResult<ExecutionAppended> missing = await context.Batches.StartExecutionAsync(created.ExecutionId + 99);
        Assert.Equal(BatchOutcome.ExecutionNotFound, missing.Outcome);
    }

    [Theory]
    [InlineData(BatchStatus.COMPLETADO)]
    [InlineData(BatchStatus.CANCELADO)]
    public async Task Close_execution_leaves_the_terminal_state_on_the_batch_and_the_execution(BatchStatus terminal)
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        await context.Batches.StartExecutionAsync(created.ExecutionId);

        BatchOperationResult closed = await context.Batches.CloseExecutionAsync(created.ExecutionId, terminal);

        Assert.True(closed.Succeeded, closed.Detail);

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(terminal, batch.Status);

        ExecutionSnapshot execution = Assert.Single(batch.Executions);
        Assert.Equal(terminal, execution.Status);
        Assert.NotNull(execution.FinishedAtUtc);

        // Un lote cerrado ya no es el lote activo: no hay trabajo en curso.
        Assert.Null(await context.State.GetActiveBatchAsync());
    }

    [Fact]
    public async Task Close_execution_rejects_a_state_that_is_not_terminal()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        BatchOperationResult closed = await context.Batches.CloseExecutionAsync(created.ExecutionId, BatchStatus.LISTO);

        Assert.Equal(BatchOutcome.InvalidData, closed.Outcome);
        Assert.Equal(BatchStatus.LISTO, (await context.State.GetBatchAsync(created.BatchId))!.Status);
    }

    [Fact]
    public async Task Active_batch_is_the_most_recent_one_without_a_terminal_state()
    {
        using BatchTestContext context = new();
        BatchCreated first = await context.CreateAsync();
        BatchCreated second = await context.CreateAsync(2026, 12);

        BatchStateSnapshot? active = await context.State.GetActiveBatchAsync();

        Assert.NotNull(active);
        Assert.Equal(second.BatchId, active!.BatchId);
        Assert.NotEqual(first.BatchId, active.BatchId);
    }

    [Fact]
    public async Task History_is_never_overwritten_by_a_new_batch()
    {
        using BatchTestContext context = new();
        BatchCreated first = await context.CreateAsync();
        await context.RegisterAsync(first.BatchId, "8.123.456");

        BatchCreated second = await context.CreateAsync();
        await context.RegisterAsync(second.BatchId, "8.123.456");

        IReadOnlyList<BatchSummary> batches = await context.State.ListBatchesAsync();
        Assert.Equal(2, batches.Count);

        BatchOperationResult<IReadOnlyList<BatchRecipient>> firstRecipients =
            await context.Recipients.ListAsync(first.BatchId);
        BatchOperationResult<IReadOnlyList<BatchRecipient>> secondRecipients =
            await context.Recipients.ListAsync(second.BatchId);

        Assert.Equal("8123456", Assert.Single(firstRecipients.Value!).Cedula);
        Assert.Equal("8123456", Assert.Single(secondRecipients.Value!).Cedula);
    }

    [Fact]
    public async Task Database_file_survives_a_new_context_as_persisted_state()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        // El estado quedó en disco, no en memoria: se lee desde una conexión nueva.
        Assert.True(File.Exists(context.Database.DatabasePath));
        Assert.Equal(created.Code, (await context.State.GetBatchAsync(created.BatchId))!.Code);
    }
}
