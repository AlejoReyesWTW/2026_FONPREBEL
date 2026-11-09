using FonPrebel.Application.Interfaces;
using FonPrebel.Presentation.ViewModels;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S26 (§2.9.2 r12): un guardado cuyos campos son válidos pero deja la configuración incompleta para
/// operar se persiste, pero antes pide confirmación en un diálogo modal. Si el analista cancela no se
/// escribe nada y los valores quedan en pantalla; con la configuración completa el diálogo no aparece.
/// </summary>
public sealed class SettingsIncompleteSaveTests
{
    [Fact]
    public async Task A_complete_configuration_is_saved_without_asking_for_confirmation()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        context.FillValidValues();

        bool saved = await context.Settings.SaveAsync();

        Assert.True(saved);
        Assert.Equal(0, context.ConfirmationDialog.RequestCount);
        Assert.True(context.WriteCount > 0);
        Assert.Equal(SettingsViewModel.SavedMessage, context.Settings.StatusMessage);
        Assert.False(context.Settings.HasStatusError);
    }

    [Fact]
    public async Task A_valid_but_incomplete_configuration_asks_for_confirmation_and_persists_when_confirmed()
    {
        using ConfigurationTestContext context = new();

        // Sin credencial guardada la configuración no alcanza para ejecutar el aplicativo (Anexo D.2).
        await context.LoadAsync();
        context.FillValidValues();
        context.ConfirmationDialog.Response = true;

        bool saved = await context.Settings.SaveAsync();

        Assert.True(saved);
        Assert.Equal(1, context.ConfirmationDialog.RequestCount);
        Assert.Equal(SettingsViewModel.IncompleteSaveTitle, context.ConfirmationDialog.LastTitle);
        Assert.Contains("se va a guardar", context.ConfirmationDialog.LastMessage, StringComparison.Ordinal);
        Assert.Contains(
            "no va a poder ejecutarse correctamente",
            context.ConfirmationDialog.LastMessage,
            StringComparison.Ordinal);
        Assert.Equal("smtp.ejemplo.test", (await context.EmailStore.ReadAsync()).Value?.Host);
        Assert.Equal(SettingsViewModel.SavedMessage, context.Settings.StatusMessage);
        Assert.False(context.Settings.CanSave);
    }

    [Fact]
    public async Task Cancelling_the_confirmation_writes_nothing_and_keeps_the_typed_values()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();
        context.FillValidValues();
        context.ConfirmationDialog.Response = false;

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(1, context.ConfirmationDialog.RequestCount);
        Assert.Equal(0, context.WriteCount);
        Assert.Equal("smtp.ejemplo.test", context.Settings.Email.Host.Value);
        Assert.Equal("587", context.Settings.Email.Port.Value);
        Assert.True(context.Settings.CanSave);
        Assert.Null((await context.EmailStore.ReadAsync()).Value?.Host);
        Assert.Null((await context.ConfigurationStore.ReadTextAsync(AppConfigurationKeys.Route1)).Value);
    }
}
