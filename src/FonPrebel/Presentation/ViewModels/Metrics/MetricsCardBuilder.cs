using System.Globalization;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.ViewModels.Metrics;

/// <summary>
/// Arma el tablero de la ficha V5 a partir de los contadores leídos de SQLite. Es el único lugar donde se
/// decide qué tarjeta se muestra, con qué numerador y denominador, y cuál queda marcada como pendiente.
/// </summary>
/// <remarks>
/// Regla del §2.22 y de la ficha V5: no se inventan indicadores. Las familias que la especificación deja
/// pendientes de definir se muestran igual, sin número, con la leyenda <see cref="PendingCaption"/> y el
/// motivo; es preferible a omitirlas en silencio. Un cero es un valor válido y se muestra como cero.
/// </remarks>
internal static class MetricsCardBuilder
{
    /// <summary>Leyenda de las familias que el §2.22 deja pendientes de definir.</summary>
    public const string PendingCaption = "Pendiente de definir";

    /// <summary>Valor visible cuando no corresponde calcular un número.</summary>
    public const string NoValue = "—";

    /// <summary>Tarjetas del tablero, en el orden en que se leen de izquierda a derecha.</summary>
    public static IReadOnlyList<MetricCard> Build(MetricsTotals totals, string scopeText)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeText);

        return new[]
        {
            BatchCard(totals),
            ExecutionCard(totals),
            RecipientCard(totals, scopeText),
            SendCard(totals),
            SendSuccessCard(totals),
            PdfCard(totals),
            ProcessSuccessCard(),
            FailureCauseCard(),
            AveragePhaseTimeCard(),
            PeriodComparisonCard(),
        };
    }

    /// <summary>Formatea un contador con el separador de miles de la cultura actual.</summary>
    public static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static MetricCard BatchCard(MetricsTotals totals) =>
        new(
            "Lotes registrados",
            SymbolRegular.FolderMultiple24,
            Count(totals.BatchCount),
            totals.BatchCount == 1 ? "Lote guardado en la base local" : "Lotes guardados en la base local",
            new[]
            {
                new MetricDetail("Completados", Count(totals.CompletedBatchCount)),
                new MetricDetail("En ejecución", Count(totals.RunningBatchCount)),
                new MetricDetail("Interrumpidos", Count(totals.InterruptedBatchCount)),
                new MetricDetail("Cancelados", Count(totals.CancelledBatchCount)),
            });

    private static MetricCard ExecutionCard(MetricsTotals totals) =>
        new(
            "Ejecuciones",
            SymbolRegular.ArrowRepeatAll24,
            Count(totals.ExecutionCount),
            "Incluye las reanudaciones de cada lote",
            new[] { new MetricDetail("En curso", Count(totals.RunningExecutionCount)) });

    private static MetricCard RecipientCard(MetricsTotals totals, string scopeText) =>
        new(
            "Destinatarios",
            SymbolRegular.People24,
            Count(totals.RecipientCount),
            $"Jubilados registrados de {scopeText}",
            new[]
            {
                new MetricDetail("Con correo registrado", Count(totals.RecipientsWithEmail)),
                new MetricDetail("Sin correo", Count(totals.RecipientsWithoutEmail)),
            });

    private static MetricCard SendCard(MetricsTotals totals) =>
        new(
            "Envíos",
            SymbolRegular.Send24,
            Count(totals.SendPending),
            "Correos pendientes de enviar",
            new[]
            {
                new MetricDetail("Enviados", Count(totals.SendSent)),
                new MetricDetail("Fallidos", Count(totals.SendFailed)),
                new MetricDetail("Inciertos", Count(totals.SendUncertain)),
                new MetricDetail("En envío", Count(totals.SendSending)),
            });

    /// <summary>
    /// Porcentaje de éxito de los envíos con numerador y denominador visibles. El denominador son los
    /// envíos con resultado —enviados, fallidos e inciertos—, así un envío incierto nunca cuenta como éxito.
    /// Sin envíos con resultado no hay porcentaje: se muestra <see cref="NoValue"/>, nunca un cero falso.
    /// </summary>
    private static MetricCard SendSuccessCard(MetricsTotals totals)
    {
        int resolved = totals.SendSent + totals.SendFailed + totals.SendUncertain;
        IReadOnlyList<MetricDetail> details = new[]
        {
            new MetricDetail("Confirmados", Count(totals.SendSent)),
            new MetricDetail("Con resultado", Count(resolved)),
        };

        if (resolved == 0)
        {
            return new MetricCard(
                "Éxito de los envíos",
                SymbolRegular.CheckmarkCircle24,
                NoValue,
                "Todavía no hay envíos con resultado: no se calcula el porcentaje.",
                details);
        }

        string value = $"{Count(totals.SendSent)} de {Count(resolved)} · {Percent(totals.SendSent, resolved)} %";

        return new MetricCard(
            "Éxito de los envíos",
            SymbolRegular.CheckmarkCircle24,
            value,
            "Envíos confirmados sobre envíos con resultado",
            details);
    }

    private static MetricCard PdfCard(MetricsTotals totals) =>
        new(
            "PDFs",
            SymbolRegular.DocumentPdf24,
            Count(totals.PdfGenerated),
            "PDFs generados",
            new[]
            {
                new MetricDetail("Pendientes", Count(totals.PdfPending)),
                new MetricDetail("Fallidos", Count(totals.PdfFailed)),
            });

    private static MetricCard ProcessSuccessCard() =>
        new(
            "Éxito de los procesos",
            SymbolRegular.DataBarVertical24,
            NoValue,
            PendingCaption,
            Array.Empty<MetricDetail>(),
            IsPending: true,
            Note: "El §2.22 no define qué cuenta como proceso exitoso: no se calcula este porcentaje.");

    private static MetricCard FailureCauseCard() =>
        new(
            "Fallos por causa",
            SymbolRegular.ErrorCircle24,
            NoValue,
            PendingCaption,
            Array.Empty<MetricDetail>(),
            IsPending: true,
            Note: "La taxonomía de causas de fallo está pendiente de definir; no se agrupa por causas inventadas.");

    private static MetricCard AveragePhaseTimeCard() =>
        new(
            "Tiempo medio por fase",
            SymbolRegular.Clock24,
            NoValue,
            PendingCaption,
            Array.Empty<MetricDetail>(),
            IsPending: true,
            Note: "La consulta de estado de los lotes no expone tiempos por fase: se agrega cuando exista esa fuente.");

    private static MetricCard PeriodComparisonCard() =>
        new(
            "Comparación entre periodos",
            SymbolRegular.ChartMultiple24,
            NoValue,
            PendingCaption,
            Array.Empty<MetricDetail>(),
            IsPending: true,
            Note: "Los periodos de comparación y los gráficos están pendientes de definir en el §2.22.");

    private static string Percent(int numerator, int denominator)
    {
        double value = Math.Round(numerator * 100d / denominator, 1, MidpointRounding.AwayFromZero);

        return value.ToString("0.#", CultureInfo.CurrentCulture);
    }
}
