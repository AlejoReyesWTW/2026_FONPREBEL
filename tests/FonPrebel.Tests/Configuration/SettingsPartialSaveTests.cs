using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.ViewModels.Config;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S29 (§2.9.2 r16-r17): un obligatorio vacío no es un valor inválido. Vacío no impide guardar —cuenta
/// como configuración incompleta y dispara el aviso del punto 12—, un valor escrito que no pasa su
/// comprobación sí bloquea y nunca se persiste, y el rechazo nombra los campos que lo causan con su
/// motivo. El gate de INICIAR sigue exigiendo todos los obligatorios completos y válidos.
/// </summary>
public sealed class SettingsPartialSaveTests
{
    [Fact]
    public async Task Saving_with_the_email_filled_and_the_four_routes_empty_asks_for_confirmation_and_persists()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        // Escenario reportado por el analista: completa sólo el correo y deja las cuatro rutas vacías.
        context.Settings.Email.Host.Value = "smtp.ejemplo.test";
        context.Settings.Email.Port.Value = "587";
        context.Settings.Email.Security.Value = "StartTls";
        context.Settings.Email.Sender.Value = "seguros@ejemplo.test";
        context.Settings.Email.SendTimeout.Value = "30";
        await WaitForValidationsAsync(context.Settings);

        Assert.All(
            new[]
            {
                context.Settings.Routes.Route1,
                context.Settings.Routes.Route2,
                context.Settings.Routes.Route3,
                context.Settings.Routes.Route4,
            },
            route => Assert.Equal(string.Empty, route.Value));

        bool saved = await context.Settings.SaveAsync();

        Assert.True(saved, "un obligatorio vacío no puede bloquear el guardado (§2.9.2 r16).");
        Assert.Equal(1, context.ConfirmationDialog.RequestCount);
        Assert.Equal(SettingsViewModel.IncompleteSaveTitle, context.ConfirmationDialog.LastTitle);
        Assert.Equal("smtp.ejemplo.test", (await context.EmailStore.ReadAsync()).Value?.Host);
        Assert.Equal(587, (await context.EmailStore.ReadAsync()).Value?.Port);
        Assert.Null((await context.ConfigurationStore.ReadTextAsync(AppConfigurationKeys.Route1)).Value);
        Assert.Equal(SettingsViewModel.SavedMessage, context.Settings.StatusMessage);
        Assert.False(context.Settings.HasStatusError);
    }

    [Fact]
    public async Task A_written_route_that_does_not_exist_blocks_the_save_and_the_message_names_it()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        context.FillValidValues();

        string missing = Path.Combine(context.Database.Paths.Root, "consolidado-inexistente.xlsx");
        context.Settings.Routes.Route1.BeginEditCommand.Execute(null);
        context.Settings.Routes.Route1.Value = missing;
        await context.Settings.Routes.Route1.PendingValidation;

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved, "un valor escrito inválido sí impide guardar (§2.9.2 r16).");
        Assert.Equal(0, context.WriteCount);
        Assert.Equal(0, context.ConfirmationDialog.RequestCount);
        Assert.True(context.Settings.HasStatusError);
        Assert.Contains(FileRouteValidation.Route1Parameter, context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("no existe un archivo en esa ruta", context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(FileRouteValidation.Route2Parameter, context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.Null((await context.ConfigurationStore.ReadTextAsync(AppConfigurationKeys.Route1)).Value);
    }

    [Fact]
    public async Task An_empty_required_field_does_not_block_the_save_and_is_never_green()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel route1 = context.Settings.Routes.Route1;

        Assert.False(route1.IsValid);
        Assert.False(route1.IsBlocking, "un obligatorio vacío no impide guardar (§2.9.2 r16).");
        Assert.False(route1.ShowsSuccess, "un campo vacío nunca se muestra en verde.");
        Assert.Contains("obligatoria", route1.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lo que reportó el analista: editaba, borraba lo escrito y el campo quedaba en verde. Vacío no
    /// bloquea el guardado, pero se señala en rojo como incompleto.
    /// </summary>
    [Fact]
    public async Task Clearing_a_field_after_editing_leaves_it_red_and_never_green()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel route1 = context.Settings.Routes.Route1;
        route1.BeginEditCommand.Execute(null);
        route1.Value = Path.Combine(context.Database.Paths.Root, "consolidado.xlsx");
        await route1.PendingValidation;

        route1.Value = string.Empty;
        await route1.PendingValidation;

        Assert.True(route1.ShowsError, "un campo vacío se señala en rojo como incompleto.");
        Assert.False(route1.ShowsSuccess, "un campo vacío nunca se muestra en verde.");
        Assert.False(route1.IsBlocking, "pero no impide guardar.");
    }

    [Fact]
    public async Task The_start_gate_keeps_requiring_every_required_field_filled_and_valid()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        context.FillValidValues();
        context.Settings.Routes.Route4.Value = string.Empty;
        await WaitForValidationsAsync(context.Settings);

        Assert.True(await context.Settings.SaveAsync(), "Vaciar una ruta no impide guardar el resto.");
        Assert.False(
            context.Gate.IsConfigurationComplete,
            "Con un obligatorio vacío el aplicativo no puede ejecutarse: INICIAR sigue cerrado.");

        context.Settings.Routes.Route4.Value = context.Route4Folder;
        await WaitForValidationsAsync(context.Settings);

        Assert.True(await context.Settings.SaveAsync());
        Assert.True(context.Gate.IsConfigurationComplete);
    }

    [Fact]
    public async Task A_save_waits_for_a_field_that_is_still_validating_instead_of_rejecting()
    {
        using ConfigurationTestContext context = new(TimeSpan.FromMilliseconds(80));
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        context.FillValidValues();
        await WaitForValidationsAsync(context.Settings);

        context.Settings.Email.Host.Value = "smtp.otro.test";
        Assert.True(context.Settings.Email.Host.IsValidating);

        bool saved = await context.Settings.SaveAsync();

        Assert.True(saved);
        Assert.Equal(0, context.ConfirmationDialog.RequestCount);
        Assert.True(context.Settings.Email.Host.IsValid);
        Assert.Equal("smtp.otro.test", (await context.EmailStore.ReadAsync()).Value?.Host);
    }

    private static async Task WaitForValidationsAsync(SettingsViewModel settings)
    {
        foreach (ValidatedFieldViewModel field in settings.AllFields)
        {
            await field.PendingValidation;
        }
    }
}
