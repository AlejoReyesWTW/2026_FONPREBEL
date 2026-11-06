using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.Email;
using FonPrebel.Infrastructure.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// Base temporal con los almacenes reales de configuración y una carpeta propia que hace de "servidor de
/// archivos". Ninguna prueba toca <c>%LOCALAPPDATA%</c> ni el servidor real: las rutas se configuran en
/// la base temporal, igual que lo haría el analista desde la pantalla.
/// </summary>
internal sealed class ConnectionProbeTestContext : IDisposable
{
    public ConnectionProbeTestContext()
    {
        Database = new TestDatabase();
        Database.Migrate();

        ConfigurationStore = new AppConfigurationStore(Database.Factory, NullLogger<AppConfigurationStore>.Instance);
        EmailStore = new EmailSettingsStore(Database.Factory, NullLogger<EmailSettingsStore>.Instance);

        Root = Path.Combine(Database.Paths.Root, "servidor");
        Directory.CreateDirectory(Root);
    }

    public TestDatabase Database { get; }

    public AppConfigurationStore ConfigurationStore { get; }

    public EmailSettingsStore EmailStore { get; }

    /// <summary>Carpeta raíz que representa el servidor de archivos de la prueba.</summary>
    public string Root { get; }

    public FileServerConnectionProbe CreateFileProbe() => new(ConfigurationStore);

    public EmailConnectionProbe CreateEmailProbe(IEmailConnectionCheck? check = null) => new(EmailStore, check);

    /// <summary>Crea un archivo real dentro de la raíz y devuelve su ruta.</summary>
    public string CreateFile(string name)
    {
        string path = Path.Combine(Root, name);
        File.WriteAllText(path, "contenido de prueba");

        return path;
    }

    /// <summary>Crea una carpeta real dentro de la raíz y devuelve su ruta.</summary>
    public string CreateFolder(string name)
    {
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>Ruta que no existe: sirve para comprobar el estado rojo.</summary>
    public string MissingPath(string name) => Path.Combine(Root, name);

    public async Task SetRouteAsync(string key, string? value)
    {
        ConfigurationWriteResult result = await ConfigurationStore.WriteTextAsync(key, value);

        Assert.True(result.Succeeded, $"La ruta {key} debe guardarse en la base temporal.");
    }

    public async Task SetEmailAsync(string? host, int? port)
    {
        ConfigurationWriteResult result = await EmailStore.WriteAsync(new EmailSettings(host, port, null, null, null));

        Assert.True(result.Succeeded, "La configuración de correo debe guardarse en la base temporal.");
    }

    public async Task SetAutoCheckMinutesAsync(int? minutes)
    {
        ConfigurationWriteResult result = await ConfigurationStore.WriteIntAsync(
            AppConfigurationKeys.AutoCheckMinutes,
            minutes);

        Assert.True(result.Succeeded, "El intervalo de verificación debe guardarse en la base temporal.");
    }

    public async Task SetSlowThresholdAsync(int? milliseconds)
    {
        ConfigurationWriteResult result = await ConfigurationStore.WriteIntAsync(
            AppConfigurationKeys.SlowThresholdMs,
            milliseconds);

        Assert.True(result.Succeeded, "El umbral de lentitud debe guardarse en la base temporal.");
    }

    public void Dispose() => Database.Dispose();
}
