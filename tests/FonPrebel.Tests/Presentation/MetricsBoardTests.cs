using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.ViewModels.Metrics;
using FonPrebel.Tests.Batches;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// Tablero de Métricas (ficha V5, §2.22): los números salen de SQLite, un cero es un cero, el porcentaje de
/// éxito muestra su denominador y las familias que el spec deja pendientes se muestran con su motivo en
/// lugar de con una cifra inventada.
/// </summary>
public sealed class MetricsBoardTests
{
    private const string Scope = "todos los lotes";

    [Fact]
    public void Families_the_spec_leaves_undefined_are_shown_as_pending_with_a_reason_instead_of_a_number()
    {
        IReadOnlyList<MetricCard> cards = MetricsCardBuilder.Build(MetricsTotals.Empty, Scope);

        MetricCard[] pending = cards.Where(card => card.IsPending).ToArray();

        Assert.Equal(4, pending.Length);

        foreach (MetricCard card in pending)
        {
            Assert.Equal(MetricsCardBuilder.NoValue, card.Value);
            Assert.Equal(MetricsCardBuilder.PendingCaption, card.Caption);
            Assert.False(string.IsNullOrWhiteSpace(card.Note), $"La tarjeta '{card.Title}' no explica por qué está pendiente.");
        }
    }

    [Fact]
    public void A_real_zero_is_shown_as_zero_but_the_success_rate_is_never_a_false_zero()
    {
        IReadOnlyList<MetricCard> cards = MetricsCardBuilder.Build(MetricsTotals.Empty, Scope);

        Assert.Equal("0", Card(cards, "Lotes registrados").Value);
        Assert.Equal("0", Card(cards, "Envíos").Value);

        MetricCard success = Card(cards, "Éxito de los envíos");

        Assert.Equal(MetricsCardBuilder.NoValue, success.Value);
        Assert.Contains("no se calcula el porcentaje", success.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void An_uncertain_send_is_counted_in_the_denominator_and_never_as_a_success()
    {
        MetricsTotals totals = MetricsTotals.Empty with { SendSent = 2, SendUncertain = 1 };

        MetricCard success = Card(MetricsCardBuilder.Build(totals, Scope), "Éxito de los envíos");

        Assert.StartsWith("2 de 3", success.Value, StringComparison.Ordinal);
        Assert.Equal("2", Detail(success, "Confirmados"));
        Assert.Equal("3", Detail(success, "Con resultado"));
    }

    [Fact]
    public async Task The_screen_counts_the_batches_and_recipients_that_exist_in_the_database()
    {
        using BatchTestContext context = new();
        var created = await context.CreateAsync();

        await context.RegisterAsync(created.BatchId, "1032456789");

        MetricsViewModel screen = new(context.State, context.Recipients);

        await screen.RefreshCommand.ExecuteAsync(null);

        Assert.False(screen.HasReadError);
        Assert.NotEmpty(screen.Cards);
        Assert.Equal("1", Card(screen.Cards, "Lotes registrados").Value);
        Assert.Equal("1", Card(screen.Cards, "Destinatarios").Value);
        Assert.Equal("1", Detail(Card(screen.Cards, "Destinatarios"), "Con correo registrado"));
    }

    private static MetricCard Card(IReadOnlyList<MetricCard> cards, string title) =>
        cards.Single(card => card.Title == title);

    private static string Detail(MetricCard card, string label) =>
        card.Details.Single(detail => detail.Label == label).Value;
}
