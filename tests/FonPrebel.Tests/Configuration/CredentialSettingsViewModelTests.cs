using System.Reflection;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.Security;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.ViewModels.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S7 y §3 del diseño de configuración: la credencial guardada nunca se muestra; el modo edición elige
/// el flujo según el estado (alta, reingreso por cambio de perfil o reemplazo con la anterior), el
/// guardado se informa sin afirmar validación en el servidor y una base inaccesible se muestra como aviso.
/// </summary>
public sealed class CredentialSettingsViewModelTests
{
    private const string StoredSecret = "contrasena-guardada-de-prueba";

    private const string NewSecret = "contrasena-nueva-de-prueba";

    private static readonly DateTime UpdatedUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_successful_replacement_is_reported_as_saved_without_server_validation()
    {
        using ConfigurationTestContext context = new();
        SeedCredential(context);

        await context.LoadAsync();

        Assert.True(context.Settings.Credentials.HasCredential);

        bool replaced = await ReplaceAsync(context, StoredSecret, NewSecret, NewSecret);

        Assert.True(replaced);
        Assert.Equal(CredentialSettingsViewModel.SavedWithoutValidationMessage, context.Settings.Credentials.StatusMessage);
        Assert.Equal("Credencial guardada, sin validar en el servidor.", context.Settings.Credentials.StatusMessage);
        Assert.False(context.Settings.Credentials.HasStatusError);
        Assert.False(context.Settings.Credentials.IsEditing);
        Assert.True(context.Settings.Credentials.HasCredential);

        Assert.True(await context.CredentialService.VerifyAsync(NewSecret));
        Assert.False(await context.CredentialService.VerifyAsync(StoredSecret));
    }

    [Fact]
    public async Task A_wrong_previous_password_keeps_the_stored_credential()
    {
        using ConfigurationTestContext context = new();
        SeedCredential(context);
        await context.LoadAsync();

        bool replaced = await ReplaceAsync(context, "otra-contrasena", NewSecret, NewSecret);

        Assert.False(replaced);
        Assert.True(context.Settings.Credentials.HasStatusError);
        Assert.Contains(CredentialValidation.PreviousParameter, context.Settings.Credentials.StatusMessage, StringComparison.Ordinal);
        Assert.True(await context.CredentialService.VerifyAsync(StoredSecret));
    }

    [Fact]
    public async Task The_edit_mode_requires_the_three_fields_and_the_confirmation_to_match()
    {
        using ConfigurationTestContext context = new();
        SeedCredential(context);
        await context.LoadAsync();

        Assert.False(await ReplaceAsync(context, string.Empty, NewSecret, NewSecret));
        Assert.Contains(CredentialValidation.PreviousParameter, context.Settings.Credentials.StatusMessage, StringComparison.Ordinal);

        Assert.False(await ReplaceAsync(context, StoredSecret, NewSecret, "otra-cosa"));
        Assert.Contains(CredentialValidation.ConfirmationParameter, context.Settings.Credentials.StatusMessage, StringComparison.Ordinal);

        Assert.True(await context.CredentialService.VerifyAsync(StoredSecret));
    }

    [Fact]
    public async Task A_not_recoverable_secret_warns_and_keeps_the_start_gate_closed()
    {
        using ConfigurationTestContext context = new();

        using (AppDbContext database = context.Database.CreateContext())
        {
            database.EmailConfigurations.Add(new EmailConfiguration
            {
                Id = 1,
                PasswordCipher = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                PasswordUpdatedAtUtc = UpdatedUtc,
                UpdatedAtUtc = UpdatedUtc,
            });
            database.SaveChanges();
        }

        await context.LoadAsync();
        await context.FillAndSaveAsync();

        Assert.True(context.Settings.Credentials.IsNotRecoverable);
        Assert.Equal(CredentialSettingsViewModel.NotRecoverableWarning, context.Settings.Credentials.StateText);
        Assert.False(context.Settings.Credentials.AllowsStart);
        Assert.False(context.Execution.CanStart);
    }

    [Fact]
    public async Task The_stored_secret_is_never_exposed_by_any_property()
    {
        using ConfigurationTestContext context = new();
        SeedCredential(context);

        await context.LoadAsync();
        await ReplaceAsync(context, StoredSecret, NewSecret, NewSecret);

        Assert.Empty(context.Settings.Credentials.StoredCredentialMask);
        AssertNoPropertyContains(context.Settings, StoredSecret);
        AssertNoPropertyContains(context.Settings, NewSecret);
        AssertNoPropertyContains(context.Settings.Credentials, StoredSecret);
        AssertNoPropertyContains(context.Settings.Credentials, NewSecret);
    }

    [Fact]
    public async Task An_inaccessible_database_is_reported_without_throwing()
    {
        ThrowingContextFactory factory = new();
        CredentialSettingsViewModel credentials = new(
            new EmailCredentialService(factory, new DpapiCredentialProvider(), NullLogger<EmailCredentialService>.Instance));

        await credentials.RefreshAsync();

        Assert.True(credentials.HasStorageError);
        Assert.Equal(CredentialSettingsViewModel.StorageErrorMessage, credentials.StatusMessage);

        credentials.BeginEditCommand.Execute(null);
        credentials.PreviousPassword = StoredSecret;
        credentials.NewPassword = NewSecret;
        credentials.ConfirmationPassword = NewSecret;

        bool replaced = await credentials.ReplaceAsync();

        Assert.False(replaced);
        Assert.True(credentials.HasStorageError);
    }

    [Fact]
    public async Task The_first_registration_asks_the_password_twice_and_saves_it()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        CredentialSettingsViewModel credentials = context.Settings.Credentials;

        Assert.Equal(EmailCredentialState.NotConfigured, credentials.State);
        Assert.True(credentials.IsFirstRegistration);
        Assert.False(credentials.RequiresPreviousPassword);
        Assert.Equal("Contraseña", credentials.NewPasswordLabel);
        Assert.Equal("Confirmar contraseña", credentials.ConfirmationLabel);

        bool saved = await RegisterAsync(context, NewSecret, NewSecret);

        Assert.True(saved);
        Assert.Equal(CredentialSettingsViewModel.SavedWithoutValidationMessage, credentials.StatusMessage);
        Assert.False(credentials.HasStatusError);
        Assert.False(credentials.IsEditing);
        Assert.True(credentials.HasCredential);
        Assert.True(credentials.AllowsStart);

        Assert.True(await context.CredentialService.VerifyAsync(NewSecret));

        using AppDbContext database = context.Database.CreateContext();
        EmailConfiguration stored = Assert.Single(database.EmailConfigurations.AsNoTracking().ToList());
        Assert.Equal(1, stored.Id);
    }

    [Fact]
    public async Task The_start_gate_stays_closed_until_the_first_credential_is_registered()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();
        await context.FillAndSaveAsync();

        Assert.True(context.Settings.Credentials.IsFirstRegistration);
        Assert.False(context.Settings.Credentials.AllowsStart);
        Assert.False(context.Execution.CanStart);
        Assert.True(context.Execution.ShowsIncompleteConfigurationWarning);

        Assert.True(await RegisterAsync(context, NewSecret, NewSecret));

        Assert.True(context.Settings.Credentials.HasCredential);
        Assert.True(context.Execution.CanStart);
        Assert.False(context.Execution.ShowsIncompleteConfigurationWarning);
    }

    [Fact]
    public async Task A_not_recoverable_credential_is_reentered_without_the_previous_password()
    {
        using ConfigurationTestContext context = new();
        context.SeedUndecryptableCredential();
        await context.LoadAsync();

        CredentialSettingsViewModel credentials = context.Settings.Credentials;

        Assert.True(credentials.IsNotRecoverable);
        Assert.True(credentials.ShowsNotRecoverableWarning);
        Assert.False(credentials.RequiresPreviousPassword);
        Assert.Equal("Contraseña nueva", credentials.NewPasswordLabel);
        Assert.Equal("Confirmar contraseña", credentials.ConfirmationLabel);

        bool saved = await RegisterAsync(context, NewSecret, NewSecret);

        Assert.True(saved);
        Assert.Equal(CredentialSettingsViewModel.SavedWithoutValidationMessage, credentials.StatusMessage);
        Assert.False(credentials.HasStatusError);
        Assert.True(credentials.HasCredential);
        Assert.True(await context.CredentialService.VerifyAsync(NewSecret));

        using AppDbContext database = context.Database.CreateContext();
        Assert.Single(database.EmailConfigurations.AsNoTracking().ToList());
    }

    [Fact]
    public async Task The_available_flow_still_requires_the_previous_password()
    {
        using ConfigurationTestContext context = new();
        SeedCredential(context);
        await context.LoadAsync();

        CredentialSettingsViewModel credentials = context.Settings.Credentials;

        Assert.True(credentials.RequiresPreviousPassword);
        Assert.False(credentials.IsFirstRegistration);
        Assert.Equal("Contraseña nueva", credentials.NewPasswordLabel);

        Assert.False(await RegisterAsync(context, NewSecret, NewSecret));
        Assert.Contains(CredentialValidation.PreviousParameter, credentials.StatusMessage, StringComparison.Ordinal);
        Assert.True(await context.CredentialService.VerifyAsync(StoredSecret));
    }

    [Fact]
    public async Task A_mismatched_confirmation_neither_calls_the_service_nor_writes()
    {
        CountingCredentialService registration = new(EmailCredentialState.NotConfigured);
        CredentialSettingsViewModel first = new(registration);

        await first.RefreshAsync();
        first.BeginEditCommand.Execute(null);
        first.NewPassword = NewSecret;
        first.ConfirmationPassword = "otra-contrasena";

        Assert.False(await first.ReplaceAsync());
        Assert.Equal(0, registration.Calls);
        Assert.Contains(first.ConfirmationLabel, first.StatusMessage, StringComparison.Ordinal);

        CountingCredentialService replacement = new(EmailCredentialState.Available);
        CredentialSettingsViewModel second = new(replacement);

        await second.RefreshAsync();
        second.BeginEditCommand.Execute(null);
        second.PreviousPassword = StoredSecret;
        second.NewPassword = NewSecret;
        second.ConfirmationPassword = "otra-contrasena";

        Assert.False(await second.ReplaceAsync());
        Assert.Equal(0, replacement.Calls);
    }

    [Fact]
    public async Task A_mismatched_confirmation_leaves_the_database_untouched()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        Assert.False(await RegisterAsync(context, NewSecret, "otra-contrasena"));
        Assert.Equal(EmailCredentialState.NotConfigured, context.Settings.Credentials.State);

        using AppDbContext database = context.Database.CreateContext();
        Assert.Empty(database.EmailConfigurations.AsNoTracking().ToList());
    }

    private static async Task<bool> RegisterAsync(
        ConfigurationTestContext context,
        string newSecret,
        string confirmation)
    {
        CredentialSettingsViewModel credentials = context.Settings.Credentials;

        if (!credentials.IsEditing)
        {
            credentials.BeginEditCommand.Execute(null);
        }

        credentials.PreviousPassword = string.Empty;
        credentials.NewPassword = newSecret;
        credentials.ConfirmationPassword = confirmation;

        return await credentials.ReplaceAsync();
    }
    private sealed class CountingCredentialService : IEmailCredentialService
    {
        private readonly EmailCredentialState _state;

        public CountingCredentialService(EmailCredentialState state) => _state = state;

        public int Calls { get; private set; }

        public Task<EmailCredentialStatus> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailCredentialStatus(_state, null));

        public Task<bool> VerifyAsync(string candidate, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(true);
        }

        public Task<CredentialReplaceResult> TryReplaceAsync(
            string previous,
            string newSecret,
            CancellationToken cancellationToken = default) => CountAndReplace();

        public Task<CredentialReplaceResult> TryRegisterAsync(
            string newSecret,
            CancellationToken cancellationToken = default) => CountAndReplace();

        public Task<CredentialReplaceResult> TryReenterAsync(
            string newSecret,
            CancellationToken cancellationToken = default) => CountAndReplace();

        private Task<CredentialReplaceResult> CountAndReplace()
        {
            Calls++;
            return Task.FromResult(new CredentialReplaceResult(CredentialReplaceOutcome.Replaced));
        }
    }

    private static async Task<bool> ReplaceAsync(
        ConfigurationTestContext context,
        string previous,
        string newSecret,
        string confirmation)
    {
        CredentialSettingsViewModel credentials = context.Settings.Credentials;

        if (!credentials.IsEditing)
        {
            credentials.BeginEditCommand.Execute(null);
        }

        credentials.PreviousPassword = previous;
        credentials.NewPassword = newSecret;
        credentials.ConfirmationPassword = confirmation;

        return await credentials.ReplaceAsync();
    }

    private static void SeedCredential(ConfigurationTestContext context)
    {
        DpapiCredentialProvider provider = new();
        byte[] cipher = provider.Protect(StoredSecret).Value!;

        using AppDbContext database = context.Database.CreateContext();
        database.EmailConfigurations.Add(new EmailConfiguration
        {
            Id = 1,
            PasswordCipher = cipher,
            PasswordUpdatedAtUtc = UpdatedUtc,
            UpdatedAtUtc = UpdatedUtc,
        });
        database.SaveChanges();
    }

    private static void AssertNoPropertyContains(object target, string secret)
    {
        PropertyInfo[] properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            if (property.PropertyType != typeof(string) || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string? value = property.GetValue(target) as string;

            Assert.False(
                value is not null && value.Contains(secret, StringComparison.Ordinal),
                $"La propiedad {property.Name} no puede exponer la contraseña.");
        }
    }
}
