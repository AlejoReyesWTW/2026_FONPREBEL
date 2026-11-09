using FonPrebel.Presentation.ViewModels.Config;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S27 (§2.9.2 r14): los campos de texto se muestran en sólo lectura y cada uno tiene un lápiz que
/// habilita su edición; el lápiz habilita ese campo y sólo ese, un campo deshabilitado no entra en
/// edición, y cancelar devuelve el valor almacenado sin dejar nada por guardar.
/// </summary>
public sealed class FieldEditModeTests
{
    [Fact]
    public async Task Every_text_field_is_read_only_when_the_section_opens()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        Assert.NotEmpty(context.Settings.AllFields);
        Assert.All(context.Settings.AllFields, field =>
        {
            Assert.False(field.IsEditing);
            Assert.True(field.IsReadOnly);
        });
    }

    [Fact]
    public async Task The_pencil_enables_only_that_field()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        ValidatedFieldViewModel host = context.Settings.Email.Host;

        host.BeginEditCommand.Execute(null);

        Assert.True(host.IsEditing);
        Assert.False(host.IsReadOnly);
        Assert.All(
            context.Settings.AllFields.Where(field => !ReferenceEquals(field, host)),
            other =>
            {
                Assert.False(other.IsEditing);
                Assert.True(other.IsReadOnly);
            });
    }

    [Fact]
    public async Task Cancelling_the_edit_restores_the_stored_value()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        ValidatedFieldViewModel host = context.Settings.Email.Host;
        string stored = host.Value;

        host.BeginEditCommand.Execute(null);
        host.Value = "smtp.otro.test";
        await host.PendingValidation;

        Assert.True(context.Settings.CanSave);

        host.CancelEditCommand.Execute(null);
        await host.PendingValidation;

        Assert.Equal(stored, host.Value);
        Assert.False(host.IsEditing);
        Assert.True(host.IsReadOnly);
        Assert.False(context.Settings.CanSave);
    }

    [Fact]
    public async Task A_disabled_field_does_not_enter_edit_mode()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        ValidatedFieldViewModel proxyText = context.Settings.Proxy.ProxyText;

        Assert.False(proxyText.IsEnabled);

        proxyText.BeginEditCommand.Execute(null);

        Assert.False(proxyText.IsEditing);
        Assert.True(proxyText.IsReadOnly);
        Assert.False(context.Settings.CanSave);
    }
}
