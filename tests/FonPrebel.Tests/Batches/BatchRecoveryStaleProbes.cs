using System.IO;
using System.Security.Cryptography;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// Sondas de regresión de A7 derivadas del rechequeo independiente: fijan que un lote cerrado no se
/// reanude, que un envío confirmado no vuelva a pendiente y que una pausa por inconsistencia informe
/// su propio resultado. Comparan el hash del archivo de base para probar que un rechazo no escribe.
/// </summary>
public sealed class BatchRecoveryStaleProbes
{
    [Theory]
    [InlineData(BatchStatus.COMPLETADO)]
    [InlineData(BatchStatus.CANCELADO)]
    public async Task A_closed_batch_with_a_stale_ready_execution_is_not_revived(BatchStatus terminal)
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        await context.StartWithFingerprintsAsync(created, context.WriteFile("analista.xlsx", "contenido"));
        Assert.True((await context.Resume.ValidateAsync()).CanResume);

        using (AppDbContext database = context.Database.CreateContext())
        {
            database.Batches.Single(item => item.Id == created.BatchId).Status = terminal;
            database.SaveChanges();
        }

        string hash = CheckpointAndHash(context);
        BatchStateSnapshot before = (await context.State.GetBatchAsync(created.BatchId))!;

        Assert.Null(await context.State.FindResumableAsync());
        Assert.Equal(BatchOutcome.ExecutionNotFound, (await context.Resume.ValidateAsync()).Outcome);

        BatchOperationResult<ExecutionAppended> resume = await context.Batches.ResumeAsync(created.BatchId);
        Assert.Equal(BatchOutcome.InvalidExecutionState, resume.Outcome);

        BatchStateSnapshot after = (await context.State.GetBatchAsync(created.BatchId))!;
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Executions.Count, after.Executions.Count);
        Assert.Equal(hash, CheckpointAndHash(context));
    }

    [Fact]
    public async Task A_confirmed_send_rejection_leaves_the_row_intact()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string cedula = await context.RegisterAsync(created.BatchId, "8.123.456");
        DateTime confirmed = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        Assert.True((await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.ENVIADO, confirmed))).Succeeded);

        BatchRecipient before = Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!);
        string hash = CheckpointAndHash(context);

        BatchOperationResult rejected = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.PENDIENTE));

        Assert.Equal(BatchOutcome.InvalidSendTransition, rejected.Outcome);
        BatchRecipient after = Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!);
        Assert.Equal(SendStatus.ENVIADO, after.SendStatus);
        Assert.Equal(confirmed, after.ConfirmedAtUtc);
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(before.LastAttemptAtUtc, after.LastAttemptAtUtc);
        Assert.Equal(hash, CheckpointAndHash(context));
        Assert.Empty((await context.Recipients.ListResendableAsync(created.BatchId)).Value!);
    }

    [Fact]
    public async Task A_pause_without_registered_fingerprints_reports_its_own_outcome()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        Assert.True((await context.Batches.StartExecutionAsync(created.ExecutionId)).Succeeded);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.Equal(BatchOutcome.PausedByInconsistency, result.Outcome);
        Assert.False(result.CanResume);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);
    }

    [Fact]
    public async Task A_pause_with_a_missing_input_reports_its_own_outcome()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");
        await context.StartWithFingerprintsAsync(created, file);
        File.Delete(file);

        BatchResumeValidationResult result = await context.Resume.ValidateAsync();

        Assert.Equal(BatchOutcome.PausedByInconsistency, result.Outcome);
        Assert.False(result.CanResume);
        Assert.Equal(BatchStatus.PAUSADO_POR_INCONSISTENCIA, result.BatchStatus);
    }

    /// <summary>
    /// Fuerza el checkpoint del WAL antes de comparar, para que el hash del archivo refleje los datos
    /// lógicos y no el momento en que SQLite decidió consolidarlos.
    /// </summary>
    private static string CheckpointAndHash(BatchTestContext context)
    {
        using (SqliteConnection connection = new($"Data Source={context.Database.DatabasePath}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            command.ExecuteNonQuery();
        }

        using FileStream stream = new(context.Database.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
