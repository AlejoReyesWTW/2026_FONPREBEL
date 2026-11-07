using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// S20 (Anexo F) y §5 del diseño de datos: la reanudación valida las huellas de los archivos de entrada,
/// deja el lote <c>LISTO_PARA_REANUDAR</c> o <c>PAUSADO_POR_INCONSISTENCIA</c>, y crear la ejecución nueva
/// nunca duplica el lote.
/// </summary>
public sealed class BatchResumeTests
{
    [Fact]
    public async Task Validate_with_matching_fingerprints_leaves_the_batch_ready_to_resume()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido del analista");
        await context.StartWithFingerprintsAsync(created, file);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.True(result.CanResume, result.Reason);
        Assert.Equal(created.BatchId, result.BatchId);
        Assert.Equal(created.Code, result.BatchCode);
        Assert.Equal(created.ExecutionId, result.ExecutionId);
        Assert.Equal(BatchStatus.LISTO_PARA_REANUDAR, result.BatchStatus);
        Assert.Null(result.Reason);

        InputFingerprintComparison comparison = Assert.Single(result.Comparisons);
        Assert.Equal(InputFingerprintStatus.Matches, comparison.Status);
        Assert.Equal(file, comparison.FilePath);

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(BatchStatus.LISTO_PARA_REANUDAR, batch.Status);
        Assert.Equal(BatchStatus.LISTO_PARA_REANUDAR, Assert.Single(batch.Executions).Status);
        Assert.Equal("contenido del analista", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Validate_with_a_modified_file_pauses_and_never_rewrites_it()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "original");
        await context.StartWithFingerprintsAsync(created, file);

        await File.WriteAllTextAsync(file, "contenido distinto");
        DateTime modifiedAtBeforeValidation = File.GetLastWriteTimeUtc(file);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.False(result.CanResume);

        // Pausar por inconsistencia no se informa como éxito: tiene su propio resultado (Anexo F.5).
        Assert.Equal(BatchOutcome.PausedByInconsistency, result.Outcome);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));

        InputFingerprintComparison comparison = Assert.Single(result.Comparisons);
        Assert.Equal(InputFingerprintStatus.Modified, comparison.Status);
        Assert.False(string.IsNullOrWhiteSpace(comparison.Reason));

        // El archivo del analista no se reescribe ni se toca al detectar la diferencia (Anexo F.4 y F.7).
        Assert.Equal("contenido distinto", await File.ReadAllTextAsync(file));
        Assert.Equal(modifiedAtBeforeValidation, File.GetLastWriteTimeUtc(file));

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, batch.Status);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, Assert.Single(batch.Executions).Status);
        Assert.Single(await context.State.ListBatchesAsync());
    }

    [Fact]
    public async Task Validate_with_a_missing_file_reports_it_as_not_verifiable()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);

        File.Delete(file);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.False(result.CanResume);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);

        InputFingerprintComparison comparison = Assert.Single(result.Comparisons);
        Assert.Equal(InputFingerprintStatus.Unavailable, comparison.Status);
        Assert.NotEqual(InputFingerprintStatus.Matches, comparison.Status);
        Assert.False(string.IsNullOrWhiteSpace(comparison.Reason));
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, (await context.State.GetBatchAsync(created.BatchId))!.Status);
    }

    [Fact]
    public async Task Validate_without_registered_fingerprints_pauses_instead_of_resuming()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        await context.Batches.StartExecutionAsync(created.ExecutionId);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.False(result.CanResume);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);
        Assert.Empty(result.Comparisons);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task Validate_without_an_interrupted_execution_reports_the_missing_one()
    {
        using BatchTestContext context = new();

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.False(result.CanResume);
        Assert.Equal(BatchOutcome.ExecutionNotFound, result.Outcome);
        Assert.Null(result.BatchId);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task Resume_adds_the_next_execution_of_the_same_batch_pointing_to_the_previous_one()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);
        Assert.True((await context.Resume.ValidateAsync()).CanResume);

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);

        Assert.True(resumed.Succeeded, resumed.Detail);
        Assert.Equal(created.BatchId, resumed.Value!.BatchId);
        Assert.Equal(2, resumed.Value.ExecutionNumber);
        Assert.Equal(created.ExecutionId, resumed.Value.ResumedFromExecutionId);

        // Reanudar no crea un lote nuevo (§2.8 regla 5): hay un solo lote con dos ejecuciones.
        Assert.Single(await context.State.ListBatchesAsync());

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(2, batch.Executions.Count);
        Assert.Equal(BatchStatus.EJECUTANDO, batch.Status);

        ExecutionSnapshot resumedExecution = batch.Executions[1];
        Assert.Equal(2, resumedExecution.Number);
        Assert.Equal(created.ExecutionId, resumedExecution.ResumedFromExecutionId);
        Assert.All(resumedExecution.Phases, phase => Assert.Equal(PhaseStatus.PENDIENTE, phase.Status));
    }

    [Fact]
    public async Task Resume_is_rejected_until_the_batch_was_validated()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);

        Assert.Equal(BatchOutcome.InvalidExecutionState, resumed.Outcome);
        Assert.Single((await context.State.GetBatchAsync(created.BatchId))!.Executions);

        BatchOperationResult<ExecutionAppended> missing = await context.Batches.ResumeAsync(created.BatchId + 99);
        Assert.Equal(BatchOutcome.BatchNotFound, missing.Outcome);
    }

    [Fact]
    public async Task Validate_is_idempotent_and_after_resuming_still_sees_one_batch()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);

        BatchResumeValidationResult first = await context.Resume.ValidateAsync();
        BatchResumeValidationResult second = await context.Resume.ValidateAsync();

        Assert.True(first.CanResume);
        Assert.True(second.CanResume);
        Assert.Equal(first.ExecutionId, second.ExecutionId);

        await context.Batches.ResumeAsync(created.BatchId);

        // Repetir la validación después de reanudar no duplica el lote ni sus ejecuciones (Anexo F.9).
        BatchResumeValidationResult afterResume = await context.Resume.ValidateAsync();

        Assert.True(afterResume.CanResume, afterResume.Reason);
        Assert.Single(await context.State.ListBatchesAsync());
        Assert.Equal(2, (await context.State.GetBatchAsync(created.BatchId))!.LastExecution!.Number);
    }
}
