using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Application.Validation;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.Security;
using FonPrebel.Presentation.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S18 y Anexo D: el botón INICIAR sólo se habilita con toda la configuración obligatoria válida, el
/// aviso literal aparece cuando está incompleta y el gate se vuelve a cerrar si un valor se invalida.
/// S6: ningún parámetro se muestra con un valor inventado.
/// </summary>
public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task The_start_gate_is_closed_with_an_incomplete_configuration()
    {
        using ConfigurationTestContext context = new();

        await context.LoadAsync();

        Assert.False(context.Execution.CanStart);
        Assert.True(context.Execution.ShowsIncompleteConfigurationWarning);
        Assert.Equal(
            "Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso.",
            context.Execution.IncompleteConfigurationWarningText);
        Assert.False(context.Gate.IsConfigurationComplete);
    }

    [Fact]
    public async Task The_start_gate_opens_when_all_mandatory_configuration_is_valid_and_saved()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();

        await context.FillAndSaveAsync();

        Assert.True(context.Execution.CanStart);
        Assert.False(context.Execution.ShowsIncompleteConfigurationWarning);
        Assert.Equal(SettingsViewModel.SavedMessage, context.Settings.StatusMessage);
        Assert.False(context.Settings.HasStatusError);
    }

    [Fact]
    public async Task The_start_gate_closes_again_when_a_value_is_invalidated()
    {
        using ConfigurationTestContext context = new();
        context.SeedCredential("contrasena-de-prueba");
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        Assert.True(context.Execution.CanStart);

        context.Settings.Email.Port.Value = "0";
        await context.Settings.Email.Port.PendingValidation;

        Assert.False(context.Execution.CanStart);
        Assert.True(context.Execution.ShowsIncompleteConfigurationWarning);
    }

    [Fact]
    public async Task An_invalid_field_message_contains_the_parameter_name()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        context.Settings.Email.Port.Value = "abc";
        await context.Settings.Email.Port.PendingValidation;

        Assert.False(context.Settings.Email.Port.IsValid);
        Assert.True(context.Settings.Email.Port.ShowsError);
        Assert.Contains(EmailSettingsValidation.PortParameter, context.Settings.Email.Port.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_with_an_invalid_value_neither_persists_nor_reports_success()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        context.FillValidValues();
        context.Settings.Email.Port.Value = "70000";

        bool saved = await context.Settings.SaveAsync();

        Assert.False(saved);
        Assert.StartsWith(SettingsViewModel.SaveBlockedHeader, context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.Contains(EmailSettingsValidation.PortParameter, context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("entre 1 y 65535", context.Settings.StatusMessage, StringComparison.Ordinal);
        Assert.True(context.Settings.HasStatusError);

        ConfigurationReadResult<EmailSettings> stored = await context.EmailStore.ReadAsync();

        Assert.Null(stored.Value?.Port);
        Assert.False(context.Execution.CanStart);
    }

    [Fact]
    public async Task Only_the_confirmed_values_are_shown_after_the_first_start()
    {
        using ConfigurationTestContext context = new();
        await context.ConfigurationStore.EnsureDefaultsAsync();

        await context.LoadAsync();

        Assert.Equal("2", context.Settings.Intervals.AutoCheckMinutes.Value);
        Assert.Equal(string.Empty, context.Settings.Intervals.SlowThresholdMs.Value);
        Assert.Equal(string.Empty, context.Settings.Intervals.PdfWorkers.Value);
        Assert.Equal(string.Empty, context.Settings.Intervals.EmailWorkers.Value);
        Assert.Equal(string.Empty, context.Settings.Email.Host.Value);
        Assert.Equal(string.Empty, context.Settings.Email.Port.Value);
        Assert.Equal(string.Empty, context.Settings.Routes.Route1.Value);
        Assert.False(context.Settings.Proxy.IsProxyEnabled);
        Assert.Equal(string.Empty, context.Settings.Proxy.ProxyText.Value);
    }

    [Fact]
    public async Task Saving_persists_the_values_in_sqlite()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        Assert.Equal("2", (await context.ConfigurationStore.ReadIntAsync(AppConfigurationKeys.AutoCheckMinutes)).Value?.ToString());
        Assert.Equal("1500", (await context.ConfigurationStore.ReadIntAsync(AppConfigurationKeys.SlowThresholdMs)).Value?.ToString());
        Assert.Equal("1", (await context.ConfigurationStore.ReadIntAsync(AppConfigurationKeys.EmailWorkers)).Value?.ToString());
        Assert.Equal(
            "seguros@ejemplo.test",
            (await context.EmailStore.ReadAsync()).Value?.SenderAddress);
        Assert.Equal(context.Route1File, (await context.ConfigurationStore.ReadTextAsync(AppConfigurationKeys.Route1)).Value);
        Assert.Equal(30, (await context.ConfigurationStore.ReadIntAsync(AppConfigurationKeys.SendTimeoutSeconds)).Value);
    }

    [Fact]
    public async Task Turning_the_proxy_off_clears_and_disables_the_text()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        context.Settings.Proxy.IsProxyEnabled = true;
        context.Settings.Proxy.ProxyText.Value = "http://proxy.ejemplo.test:8080";

        Assert.True(context.Settings.Proxy.ProxyText.IsEnabled);
        Assert.True((await ValidateProxyAsync(context.Settings)).IsValid);

        context.Settings.Proxy.IsProxyEnabled = false;

        Assert.False(context.Settings.Proxy.ProxyText.IsEnabled);
        Assert.Equal(string.Empty, context.Settings.Proxy.ProxyText.Value);
        Assert.True((await ValidateProxyAsync(context.Settings)).IsValid);
    }

    [Fact]
    public async Task An_inaccessible_database_is_shown_as_an_error_instead_of_throwing()
    {
        ThrowingContextFactory factory = new();
        ConfigurationReadinessGate gate = new();

        SettingsViewModel settings = new(
            new AppConfigurationStore(factory, NullLogger<AppConfigurationStore>.Instance),
            new EmailSettingsStore(factory, NullLogger<EmailSettingsStore>.Instance),
            new EmailCredentialService(
                factory,
                new DpapiCredentialProvider(),
                NullLogger<EmailCredentialService>.Instance),
            gate,
            new FakeConnectionMonitor(),
            new FakeConfirmationDialog(),
            NullLogger<SettingsViewModel>.Instance);

        await settings.LoadAsync();

        Assert.True(settings.HasStorageError);
        Assert.Equal(SettingsViewModel.StorageErrorMessage, settings.StatusMessage);
        Assert.True(settings.HasStatusError);
        Assert.False(gate.IsConfigurationComplete);
    }

    private static async Task<ValidationResult> ValidateProxyAsync(SettingsViewModel settings)
    {
        await settings.Proxy.ProxyText.PendingValidation;

        return settings.Proxy.ProxyText.LastResult;
    }
}
