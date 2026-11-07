using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.ValueObjects;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// S3 (§2.6 y §6 del diseño de datos): cada operación abre su propio contexto desde la factoría y ninguno
/// se comparte entre operaciones.
/// </summary>
public sealed class BatchContextUsageTests
{
    [Fact]
    public async Task Every_operation_opens_its_own_context()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string file = context.WriteFile("analista.xlsx", "contenido");

        await AssertOpensContextAsync(context, () => context.State.GetBatchAsync(created.BatchId));
        await AssertOpensContextAsync(context, () => context.State.ListBatchesAsync());
        await AssertOpensContextAsync(context, () => context.Batches.StartExecutionAsync(created.ExecutionId));
        await AssertOpensContextAsync(
            context,
            () => context.Recipients.RegisterAsync(created.BatchId, new[] { new RecipientRegistration("8123456") }));
        await AssertOpensContextAsync(
            context,
            () => context.Fingerprints.CaptureAsync(
                created.ExecutionId,
                new[] { new InputFileDescriptor(InputFileRole.AnalystFile, file) }));
        await AssertOpensContextAsync(context, () => context.Resume.ValidateAsync());

        // Ningún contexto se reutiliza: todas las instancias entregadas por la factoría son distintas.
        Assert.Equal(context.Counting.Created.Count, context.Counting.Created.Distinct().Count());
    }

    private static async Task AssertOpensContextAsync(BatchTestContext context, Func<Task> operation)
    {
        int before = context.Counting.Count;

        await operation();

        Assert.True(
            context.Counting.Count > before,
            "La operación debe abrir su propio contexto desde la factoría (S3).");
    }
}
