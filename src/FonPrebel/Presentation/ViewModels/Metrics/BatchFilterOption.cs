using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Presentation.ViewModels.Metrics;

/// <summary>
/// Una entrada del selector de lote de la ficha V5: "Todos los lotes" o un lote concreto.
/// </summary>
/// <remarks>
/// <see cref="BatchId"/> nulo es el alcance global. El selector no guarda contadores: al cambiar de entrada
/// la pantalla vuelve a leer la base, así no hay dos fuentes de verdad.
/// </remarks>
public sealed record BatchFilterOption(int? BatchId, string Code, string Display)
{
    /// <summary>Etiqueta de la entrada global, que es la que se muestra al abrir la sección.</summary>
    public const string AllBatchesDisplay = "Todos los lotes";

    /// <summary>Entrada global: no filtra por ningún lote.</summary>
    public static BatchFilterOption AllBatches { get; } = new(null, string.Empty, AllBatchesDisplay);

    /// <summary>Arma las entradas del selector: primero la global y después cada lote guardado.</summary>
    public static IReadOnlyList<BatchFilterOption> ComputeOptions(IReadOnlyList<BatchSummary> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);

        List<BatchFilterOption> options = new(batches.Count + 1) { AllBatches };

        foreach (BatchSummary batch in batches)
        {
            options.Add(new BatchFilterOption(
                batch.BatchId,
                batch.Code,
                $"{batch.Code} · {BatchStatusText.For(batch.Status)}"));
        }

        return options;
    }
}

/// <summary>
/// Rótulo visible de los estados del lote y de la ejecución (Anexo F.3). Los nombres del enum son los
/// literales del spec y quedan en español porque los ve el analista; acá sólo se presentan legibles.
/// </summary>
internal static class BatchStatusText
{
    /// <summary>Rótulo visible del estado.</summary>
    public static string For(BatchStatus status) => status switch
    {
        BatchStatus.LISTO => "Listo",
        BatchStatus.EJECUTANDO => "En ejecución",
        BatchStatus.PAUSADO_POR_INCONSISTENCIA => "Pausado por inconsistencia",
        BatchStatus.VALIDANDO_RECUPERACION => "Validando recuperación",
        BatchStatus.LISTO_PARA_REANUDAR => "Listo para reanudar",
        BatchStatus.COMPLETADO => "Completado",
        BatchStatus.CANCELADO => "Cancelado",
        _ => status.ToString(),
    };
}
