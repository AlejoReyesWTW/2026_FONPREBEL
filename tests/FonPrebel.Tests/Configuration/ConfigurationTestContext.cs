using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.Security;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.ViewModels.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Arma la configuración completa sobre una base temporal (sin tocar <c>%LOCALAPPDATA%</c>) y ofrece los
/// valores válidos de prueba, incluidos archivos y carpetas reales dentro del directorio temporal.
/// </summary>
internal sealed class ConfigurationTestContext : IDisposable
{
    public ConfigurationTestContext()
    {
        Database = new TestDatabase();
        Database.Migrate();

        ConfigurationStore = new AppConfigurationStore(Database.Factory, NullLogger<AppConfigurationStore>.Instance);
        EmailStore = new EmailSettingsStore(Database.Factory, NullLogger<EmailSettingsStore>.Instance);
        CredentialService = new EmailCredentialService(
            Database.Factory,
            new DpapiCredentialProvider(),
            NullLogger<EmailCredentialService>.Instance);
        Gate = new ConfigurationReadinessGate();

        Settings = new SettingsViewModel(
            ConfigurationStore,
            EmailStore,
            CredentialService,
            Gate,
            NullLogger<SettingsViewModel>.Instance);

        Execution = new ExecutionViewModel(Gate);

        string root = Database.Paths.Root;
        Route1File = Path.Combine(root, "consolidado-anual.xlsx");
        Route2Folder = Path.Combine(root, "polizas");
        Route3File = Path.Combine(root, "analista.xlsx");
        Route4Folder = Path.Combine(root, "pdfs");

        File.WriteAllText(Route1File, "consolidado");
        File.WriteAllText(Route3File, "analista");
        Directory.CreateDirectory(Route2Folder);
        Directory.CreateDirectory(Route4Folder);
    }

    public TestDatabase Database { get; }

    public AppConfigurationStore ConfigurationStore { get; }

    public EmailSettingsStore EmailStore { get; }

    public EmailCredentialService CredentialService { get; }

    public ConfigurationReadinessGate Gate { get; }

    public SettingsViewModel Settings { get; }

    public ExecutionViewModel Execution { get; }

    public string Route1File { get; }

    public string Route2Folder { get; }

    public string Route3File { get; }

    public string Route4Folder { get; }

    /// <summary>Carga la pantalla de configuración como lo hace el arranque de la aplicación.</summary>
    public Task LoadAsync() => Settings.LoadAsync();

    /// <summary>
    /// Deja una credencial de correo guardada y descifrable, como después del alta. Se llama antes de
    /// <see cref="LoadAsync"/>: el estado del secreto sólo se lee al cargar la pantalla.
    /// </summary>
    /// <param name="secret">Contraseña ficticia de prueba.</param>
    public void SeedCredential(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        byte[] cipher = new DpapiCredentialProvider().Protect(secret).Value!;

        using AppDbContext database = Database.CreateContext();
        database.EmailConfigurations.Add(new EmailConfiguration
        {
            Id = 1,
            PasswordCipher = cipher,
            PasswordUpdatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        database.SaveChanges();
    }

    /// <summary>
    /// Deja un contenido cifrado que no se descifra en este perfil (como tras un cambio de perfil de
    /// Windows). Se llama antes de <see cref="LoadAsync"/>.
    /// </summary>
    public void SeedUndecryptableCredential()
    {
        using AppDbContext database = Database.CreateContext();
        database.EmailConfigurations.Add(new EmailConfiguration
        {
            Id = 1,
            PasswordCipher = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            PasswordUpdatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        database.SaveChanges();
    }

    /// <summary>Escribe en los campos una configuración completa y válida de prueba.</summary>
    public void FillValidValues()
    {
        Settings.Email.Host.Value = "smtp.ejemplo.test";
        Settings.Email.Port.Value = "587";
        Settings.Email.Security.Value = "StartTls";
        Settings.Email.Sender.Value = "seguros@ejemplo.test";
        Settings.Email.Account.Value = string.Empty;
        Settings.Email.SendTimeout.Value = "30";

        Settings.Proxy.IsProxyEnabled = false;

        Settings.Routes.Route1.Value = Route1File;
        Settings.Routes.Route2.Value = Route2Folder;
        Settings.Routes.Route3.Value = Route3File;
        Settings.Routes.Route4.Value = Route4Folder;

        Settings.Intervals.AutoCheckMinutes.Value = "2";
        Settings.Intervals.SlowThresholdMs.Value = "1500";
        Settings.Intervals.PdfWorkers.Value = "2";
        Settings.Intervals.EmailWorkers.Value = "1";
    }

    /// <summary>Deja la configuración completa, válida y guardada.</summary>
    public async Task FillAndSaveAsync()
    {
        FillValidValues();

        bool saved = await Settings.SaveAsync();

        Assert.True(saved, "La configuración de prueba debe guardarse sin errores.");
    }

    public void Dispose() => Database.Dispose();
}
