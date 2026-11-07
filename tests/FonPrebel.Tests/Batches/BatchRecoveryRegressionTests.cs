using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using FonPrebel.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// Regresiones de la verificación independiente de A7 (S8, S20; Anexo F.7, F.8 y F.10.10): un lote cerrado
/// no se resucita, la ejecución que se reanuda deja de ser recuperable, un envío confirmado no vuelve a
/// pendiente y una pausa por inconsistencia no se informa como éxito.
/// </summary>
public sealed class BatchRecoveryRegressionTests
{
    [Theory]
    [InlineData(BatchStatus.COMPLETADO)]
    [InlineData(BatchStatus.CANCELADO)]
    public async Task A_closed_batch_is_never_offered_to_resume(BatchStatus terminal)
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);
        Assert.True((await context.Resume.ValidateAsync()).CanResume);

        // Segunda ejecución del mismo lote, que después se cierra en un estado terminal (Anexo F.8).
        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);
        Assert.True(resumed.Succeeded, resumed.Detail);
        BatchOperationResult closed = await context.Batches.CloseExecutionAsync(resumed.Value!.ExecutionId, terminal);
        Assert.True(closed.Succeeded, closed.Detail);

        BatchStateSnapshot before = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(terminal, before.Status);

        // Un lote cerrado no se valida ni se reanuda: no hay ejecución recuperable (Anexo F.7 y F.8).
        BatchResumeValidationResult validation = await context.Resume.ValidateAsync();

        Assert.False(validation.CanResume, validation.Reason);
        Assert.Equal(BatchOutcome.ExecutionNotFound, validation.Outcome);
        Assert.Null(await context.State.FindResumableAsync());

        BatchOperationResult<ExecutionAppended> again = await context.Batches.ResumeAsync(created.BatchId);

        Assert.Equal(BatchOutcome.InvalidExecutionState, again.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(again.Detail));

        // El lote y sus ejecuciones quedan intactos: no se agrega una ejecución ni se reescribe el estado.
        BatchStateSnapshot after = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Executions.Count, after.Executions.Count);
        Assert.Equal(
            before.Executions.Select(execution => (execution.ExecutionId, execution.Status)),
            after.Executions.Select(execution => (execution.ExecutionId, execution.Status)));
    }

    [Fact]
    public async Task Resume_makes_the_previous_execution_stop_being_recoverable()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);

        BatchResumeValidationResult first = await context.Resume.ValidateAsync();
        Assert.True(first.CanResume, first.Reason);
        Assert.Equal(created.ExecutionId, first.ExecutionId);

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);
        Assert.True(resumed.Succeeded, resumed.Detail);

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        ExecutionSnapshot previous = batch.Executions.Single(execution => execution.ExecutionId == created.ExecutionId);

        Assert.False(
            BatchStatusRules.IsRecoverable(previous.Status),
            $"La ejecución reanudada deja de ser recuperable y quedó en {previous.Status} (Anexo F.8).");
        Assert.True(BatchStatusRules.IsTerminal(previous.Status), $"Estado anterior: {previous.Status}.");
        Assert.NotNull(previous.FinishedAtUtc);

        // La ejecución recuperable del lote es la nueva, y la segunda validación apunta a ella.
        RecoverableExecution resumable = Assert.IsType<RecoverableExecution>(await context.State.FindResumableAsync());
        Assert.Equal(resumed.Value!.ExecutionId, resumable.ExecutionId);
        Assert.Equal(2, resumable.Number);

        BatchResumeValidationResult second = await context.Resume.ValidateAsync();
        Assert.True(second.CanResume, second.Reason);
        Assert.Equal(resumed.Value.ExecutionId, second.ExecutionId);
    }

    [Fact]
    public async Task The_resumed_execution_inherits_the_fingerprints_and_all_of_them_match()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string analyst = context.WriteFile("analista.xlsx", "contenido del analista");
        string policies = context.WriteFile("polizas.xlsx", "contenido de las polizas");
        await context.StartWithFingerprintsAsync(created, analyst, policies);
        Assert.True((await context.Resume.ValidateAsync()).CanResume);

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);
        Assert.True(resumed.Succeeded, resumed.Detail);

        // Anexo F.6 y F.9: lo válido se conserva, así que la ejecución nueva hereda las huellas verificadas.
        BatchOperationResult<IReadOnlyList<InputFingerprintComparison>> verified =
            await context.Fingerprints.VerifyAsync(resumed.Value!.ExecutionId);

        Assert.True(verified.Succeeded, verified.Detail);
        Assert.Equal(2, verified.Value!.Count);
        Assert.All(verified.Value, comparison => Assert.Equal(InputFingerprintStatus.Matches, comparison.Status));
        Assert.Contains(verified.Value, comparison => comparison.FilePath == analyst);
        Assert.Contains(verified.Value, comparison => comparison.FilePath == policies);
    }

    [Fact]
    public async Task A_state_change_that_cannot_write_the_execution_keeps_the_batch_untouched()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        Assert.True((await context.Batches.StartExecutionAsync(created.ExecutionId)).Succeeded);

        ExecuteSql(
            context,
            "CREATE TRIGGER probe_block_execution_update BEFORE UPDATE ON BatchExecution " +
            "BEGIN SELECT RAISE(ABORT, 'bloqueo de prueba'); END");

        BatchOperationResult changed;
        try
        {
            changed = await context.Batches.SetStateAsync(
                created.ExecutionId,
                BatchStatus.PAUSADO_POR_INCONSISTENCIA);
        }
        finally
        {
            ExecuteSql(context, "DROP TRIGGER IF EXISTS probe_block_execution_update");
        }

        // El cambio de estado toca la ejecución y el lote a la vez: si uno no se escribe, el otro tampoco.
        Assert.Equal(BatchOutcome.StorageUnavailable, changed.Outcome);

        BatchStateSnapshot batch = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(BatchStatus.EJECUTANDO, batch.Status);
        Assert.Equal(BatchStatus.EJECUTANDO, Assert.Single(batch.Executions).Status);
    }

    [Fact]
    public async Task A_confirmed_send_survives_the_resumption_and_is_not_offered_again()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string cedula = await context.RegisterAsync(created.BatchId, "8.123.456");
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);

        BatchOperationResult sent = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.ENVIADO, DateTime.UtcNow));
        Assert.True(sent.Succeeded, sent.Detail);
        Assert.True((await context.Resume.ValidateAsync()).CanResume);

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);
        Assert.True(resumed.Succeeded, resumed.Detail);

        // §2.8 regla 6: un correo confirmado no se reenvía al reanudar.
        IReadOnlyList<BatchRecipient> confirmed =
            (await context.Recipients.ListConfirmedSentAsync(created.BatchId)).Value!;
        IReadOnlyList<BatchRecipient> resendable =
            (await context.Recipients.ListResendableAsync(created.BatchId)).Value!;

        Assert.Equal(new[] { cedula }, confirmed.Select(recipient => recipient.Cedula));
        Assert.NotNull(Assert.Single(confirmed).ConfirmedAtUtc);
        Assert.DoesNotContain(resendable, recipient => recipient.Cedula == cedula);
        Assert.Empty(resendable);
    }

    [Fact]
    public async Task A_confirmed_send_cannot_go_back_to_pending()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string cedula = await context.RegisterAsync(created.BatchId, "8.123.456");

        BatchOperationResult sent = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.ENVIADO, DateTime.UtcNow));
        Assert.True(sent.Succeeded, sent.Detail);

        DateTime? confirmedAt = Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!).ConfirmedAtUtc;

        BatchOperationResult reverted = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.PENDIENTE));

        // Un envío confirmado no se convierte otra vez en "pendiente de enviar" (§2.8 reglas 6 y 12).
        Assert.Equal(BatchOutcome.InvalidSendTransition, reverted.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(reverted.Detail));

        BatchRecipient after = Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!);
        Assert.Equal(SendStatus.ENVIADO, after.SendStatus);
        Assert.Equal(confirmedAt, after.ConfirmedAtUtc);
        Assert.Contains(
            (await context.Recipients.ListConfirmedSentAsync(created.BatchId)).Value!,
            recipient => recipient.Cedula == cedula);
        Assert.Empty((await context.Recipients.ListResendableAsync(created.BatchId)).Value!);
    }

    [Fact]
    public async Task A_duplicate_batch_code_returns_duplicate_code_and_stores_no_second_row()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        DateTime now = DateTime.UtcNow;

        Batch duplicate = new()
        {
            Code = created.Code,
            PeriodYear = 2026,
            PeriodMonth = 11,
            Status = BatchStatus.LISTO,
            CreatedAtUtc = now,
            CreatedBy = "analista",
        };

        duplicate.Executions.Add(new BatchExecution
        {
            Number = 1,
            Status = BatchStatus.LISTO,
            StartedAtUtc = now,
            Phases = PhaseCatalog.CreatePending(),
        });

        BatchOperationResult<BatchCreated> stored = await context.Store.AddBatchAsync(duplicate);

        // §2.8 regla 1: el identificador es único y el choque se informa como resultado tipado.
        Assert.Equal(BatchOutcome.DuplicateCode, stored.Outcome);
        Assert.Null(stored.Value);

        BatchSummary only = Assert.Single(await context.State.ListBatchesAsync());
        Assert.Equal(created.Code, only.Code);
        Assert.Equal(1, only.ExecutionCount);
    }

    [Fact]
    public async Task A_pause_by_inconsistency_reports_its_own_outcome()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido original");
        await context.StartWithFingerprintsAsync(created, file);
        await File.WriteAllTextAsync(file, "contenido distinto");

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        // Pausar por inconsistencia no es "la validación salió bien": tiene su propio resultado (Anexo F.5).
        Assert.Equal(BatchOutcome.PausedByInconsistency, result.Outcome);
        Assert.False(result.CanResume);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Equal(
            BatchStatus.PAUSADO_POR_INCONSISTENCIA,
            (await context.State.GetBatchAsync(created.BatchId))!.Status);
    }

    [Fact]
    public async Task Resume_revalidates_the_recoverable_execution_instead_of_trusting_the_batch_status()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        await context.Batches.StartExecutionAsync(created.ExecutionId);

        // Estado que dejaba la versión con el defecto: el lote dice LISTO_PARA_REANUDAR y su única ejecución
        // ya está cerrada. La guarda del estado del lote por sí sola dejaría pasar la reanudación (Anexo F.7).
        using (AppDbContext database = context.Database.CreateContext())
        {
            Batch stored = database.Batches.Single(item => item.Id == created.BatchId);
            BatchExecution execution = database.BatchExecutions.Single(item => item.Id == created.ExecutionId);
            stored.Status = BatchStatus.LISTO_PARA_REANUDAR;
            execution.Status = BatchStatus.COMPLETADO;
            execution.FinishedAtUtc = DateTime.UtcNow;
            database.SaveChanges();
        }

        Assert.Null(await context.State.FindResumableAsync());

        BatchOperationResult<ExecutionAppended> resumed = await context.Batches.ResumeAsync(created.BatchId);

        Assert.Equal(BatchOutcome.InvalidExecutionState, resumed.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(resumed.Detail));
        Assert.Single((await context.State.GetBatchAsync(created.BatchId))!.Executions);
    }

    /// <summary>Disparador de SQLite para observar desde afuera qué escritura llega a la base.</summary>
    private static void ExecuteSql(BatchTestContext context, string sql)
    {
        using SqliteConnection connection = new($"Data Source={context.Database.DatabasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
