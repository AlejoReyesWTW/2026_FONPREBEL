using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// S3: aplica las migraciones pendientes una sola vez al iniciar, con la bitácora ya abierta, y carga
/// en memoria los parámetros de base que necesita el interceptor. Nunca borra ni recrea la base: si
/// algo falla, deja el archivo intacto y registra el error para que la aplicación lo muestre.
/// </summary>
/// <remarks>
/// Las conexiones que se abren durante <c>Migrate()</c> usan los valores por omisión de
/// <see cref="DatabaseRuntimeSettings"/>; a partir de la lectura de <c>AppConfiguration</c> el
/// interceptor aplica el tiempo de espera configurado.
/// </remarks>
public sealed class DatabaseStartupService : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DatabaseRuntimeSettings _settings;
    private readonly ILogger<DatabaseStartupService> _logger;

    public DatabaseStartupService(
        IDbContextFactory<AppDbContext> contextFactory,
        DatabaseRuntimeSettings settings,
        ILogger<DatabaseStartupService> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Indica si la base quedó lista para usarse.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>Error que impidió preparar la base, para que la aplicación lo muestre.</summary>
    public Exception? Failure { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Apply();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Aplica las migraciones pendientes y devuelve si la base quedó utilizable.</summary>
    public bool Apply()
    {
        try
        {
            using AppDbContext context = _contextFactory.CreateDbContext();

            context.Database.Migrate();
            LoadSettings(context);

            Succeeded = true;
            Failure = null;
            _logger.LogInformation("Base de datos local preparada");

            return true;
        }
        catch (Exception exception)
        {
            Succeeded = false;
            Failure = exception;
            _logger.LogError(
                exception,
                "No se pudieron aplicar las migraciones de la base de datos local. La base existente no se borró ni se recreó.");

            return false;
        }
    }

    private void LoadSettings(AppDbContext context)
    {
        Dictionary<string, string?> values = context.AppConfigurations
            .AsNoTracking()
            .Where(configuration =>
                configuration.Key == DatabaseSetup.WalEnabledKey ||
                configuration.Key == DatabaseSetup.BusyTimeoutKey)
            .Select(configuration => new { configuration.Key, configuration.Value })
            .ToDictionary(configuration => configuration.Key, configuration => configuration.Value);

        if (values.TryGetValue(DatabaseSetup.WalEnabledKey, out string? walEnabled) &&
            bool.TryParse(walEnabled, out bool enabled))
        {
            _settings.WalEnabled = enabled;
        }

        if (values.TryGetValue(DatabaseSetup.BusyTimeoutKey, out string? rawTimeout) &&
            int.TryParse(rawTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeoutMs) &&
            timeoutMs >= 0)
        {
            _settings.BusyTimeoutMs = timeoutMs;

            return;
        }

        // "Ningún parámetro tiene un valor por defecto inventado" (S6): sin valor configurado no se
        // envía el PRAGMA y el motor conserva su tiempo de espera por omisión.
        _settings.BusyTimeoutMs = null;
        _logger.LogWarning(
            "El parámetro {Key} está pendiente de configurar: se conserva el tiempo de espera por omisión de SQLite.",
            DatabaseSetup.BusyTimeoutKey);
    }
}
