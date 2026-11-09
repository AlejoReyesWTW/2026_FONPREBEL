using FonPrebel.Application.Interfaces;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// §2.9.2 r2: al guardar la configuración se ofrece comprobar las conexiones. Se ofrece y no se ejecuta
/// sola, porque una comprobación real toca la red y tarda; y cuando la sonda no se pudo hacer, la pantalla
/// informa el motivo en lugar de presentarla como un éxito.
/// </summary>
public sealed class SettingsConnectionOfferTests
{
    [Fact]
    public async Task A_successful_save_offers_the_connection_test_without_running_it()
    {
        using ConfigurationTestContext context = new();
        context.FillValidValues();

        bool saved = await context.Settings.SaveAsync();

        Assert.True(saved, "La configuración de prueba debe guardarse sin errores.");
        Assert.True(context.Settings.HasConnectionOffer);
        Assert.Empty(context.Monitor.CheckedKeys);
        Assert.False(context.Settings.HasConnectionOfferResult);
    }

    [Fact]
    public async Task An_invalid_configuration_that_is_not_saved_does_not_offer_the_test()
    {
        using ConfigurationTestContext context = new();

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved, "Guardar sin los campos obligatorios tiene que fallar.");
        Assert.False(context.Settings.HasConnectionOffer);
        Assert.Empty(context.Monitor.CheckedKeys);
    }

    [Fact]
    public async Task Accepting_the_offer_checks_every_declared_target_once_and_shows_the_result()
    {
        using ConfigurationTestContext context = new();
        context.FillValidValues();
        Assert.True(await context.Settings.SaveAsync());

        await context.Settings.OfferConnectionTestCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "file-server", "email" }, context.Monitor.CheckedKeys);
        Assert.False(context.Settings.HasConnectionOffer);
        Assert.True(context.Settings.HasConnectionOfferResult);

        string result = context.Settings.ConnectionOfferResultText;

        Assert.Contains("Servidor de archivos: Responde", result, StringComparison.Ordinal);
        Assert.Contains("Correo: Responde", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_probe_that_could_not_be_done_shows_its_reason_instead_of_a_success()
    {
        using ConfigurationTestContext context = new();
        context.FillValidValues();
        Assert.True(await context.Settings.SaveAsync());

        context.Monitor.ResultFactory = key => ConnectionProbeResult.Unchecked(
            key,
            key == "email" ? "Correo" : "Servidor de archivos",
            DateTimeOffset.UnixEpoch,
            reason: key == "email" ? "faltan el servidor o el puerto" : "sin configurar",
            message: "No se pudo comprobar.");

        await context.Settings.OfferConnectionTestCommand.ExecuteAsync(null);

        string result = context.Settings.ConnectionOfferResultText;

        Assert.Contains("Sin comprobar — sin configurar", result, StringComparison.Ordinal);
        Assert.Contains("Sin comprobar — faltan el servidor o el puerto", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Responde", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dismissing_the_offer_closes_it_and_checks_nothing()
    {
        using ConfigurationTestContext context = new();
        context.FillValidValues();
        Assert.True(await context.Settings.SaveAsync());

        context.Settings.DismissConnectionOfferCommand.Execute(null);

        Assert.False(context.Settings.HasConnectionOffer);
        Assert.False(context.Settings.HasConnectionOfferResult);
        Assert.Equal(string.Empty, context.Settings.ConnectionOfferResultText);
        Assert.Empty(context.Monitor.CheckedKeys);
    }
}
