using System.Diagnostics;
using FonPrebel.Presentation.ViewModels.Config;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S30 (§2.9.2 r18): el indicador de progreso de un campo se mantiene visible un tiempo mínimo
/// perceptible aunque la comprobación termine antes, sin dejar de ser esperable por las pruebas.
/// </summary>
public sealed class ValidationIndicatorTimingTests
{
    [Fact]
    public async Task The_indicator_stays_visible_a_minimum_perceptible_time()
    {
        using ConfigurationTestContext context = new(TimeSpan.FromMilliseconds(200));
        await context.LoadAsync();

        ValidatedFieldViewModel host = context.Settings.Email.Host;
        host.BeginEditCommand.Execute(null);

        Stopwatch watch = Stopwatch.StartNew();
        host.Value = "smtp.ejemplo.test";

        // La comprobación del servidor es instantánea, pero el indicador todavía tiene que verse.
        Assert.True(host.IsValidating);
        await host.PendingValidation;
        watch.Stop();

        Assert.False(host.IsValidating);
        Assert.True(host.IsValid);
        Assert.True(
            watch.Elapsed >= TimeSpan.FromMilliseconds(150),
            $"El indicador se vio {watch.ElapsedMilliseconds} ms y no llega a ser perceptible.");
    }

    [Fact]
    public void The_default_minimum_time_is_perceptible_and_short()
    {
        TimeSpan minimum = ValidatedFieldViewModel.DefaultMinimumIndicatorDuration;

        Assert.InRange(minimum.TotalMilliseconds, 250, 800);
    }

    [Fact]
    public async Task Without_a_configured_minimum_the_indicator_hides_as_soon_as_the_check_ends()
    {
        using ConfigurationTestContext context = new(TimeSpan.Zero);
        await context.LoadAsync();

        ValidatedFieldViewModel host = context.Settings.Email.Host;
        host.Value = "smtp.ejemplo.test";
        await host.PendingValidation;

        Assert.False(host.IsValidating);
        Assert.True(host.IsValid);
        Assert.True(host.ShowsSuccess);
    }
}
