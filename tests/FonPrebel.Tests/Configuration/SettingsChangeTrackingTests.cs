namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S26 (§2.9.2 r10, r11 y r13): el botón Guardar arranca inactivo en cada carga, se activa sólo con un
/// cambio real —el interruptor del proxy incluido—, vuelve a inactivo al deshacer el cambio y no
/// reescribe nada cuando no hay diferencia con lo almacenado. Cada guardado sobrescribe el valor
/// anterior de la clave: no hay historial de configuraciones.
/// </summary>
public sealed class SettingsChangeTrackingTests
{
    [Fact]
    public async Task The_save_button_is_disabled_right_after_loading_the_section()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");

        await context.LoadAsync();

        Assert.False(context.Settings.CanSave);
        Assert.False(context.Settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_save_button_is_disabled_again_after_reloading_the_saved_values()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        await context.Settings.LoadAsync();

        Assert.False(context.Settings.CanSave);
        Assert.False(context.Settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task One_change_enables_the_save_button_and_undoing_it_disables_it_again()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        context.Settings.Email.Host.Value = "smtp.otro.test";
        await context.Settings.Email.Host.PendingValidation;

        Assert.True(context.Settings.CanSave);
        Assert.True(context.Settings.SaveCommand.CanExecute(null));

        context.Settings.Email.Host.Value = "smtp.ejemplo.test";
        await context.Settings.Email.Host.PendingValidation;

        Assert.False(context.Settings.CanSave);
        Assert.False(context.Settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_proxy_switch_counts_as_a_change()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        context.Settings.Proxy.IsProxyEnabled = true;

        Assert.True(context.Settings.CanSave);

        context.Settings.Proxy.IsProxyEnabled = false;

        Assert.False(context.Settings.CanSave);
    }

    [Fact]
    public async Task A_save_without_changes_writes_nothing_and_shows_no_dialog()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        int writes = context.WriteCount;
        int dialogs = context.ConfirmationDialog.RequestCount;

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(writes, context.WriteCount);
        Assert.Equal(dialogs, context.ConfirmationDialog.RequestCount);
    }

    [Fact]
    public async Task Each_save_overwrites_the_previous_value_without_history()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        context.Settings.Email.Host.Value = "smtp.nuevo.test";
        await context.Settings.Email.Host.PendingValidation;

        Assert.True(await context.Settings.SaveAsync());
        Assert.Equal("smtp.nuevo.test", (await context.EmailStore.ReadAsync()).Value?.Host);

        await context.Settings.LoadAsync();

        Assert.Equal("smtp.nuevo.test", context.Settings.Email.Host.Value);
        Assert.False(context.Settings.CanSave);
    }
}
