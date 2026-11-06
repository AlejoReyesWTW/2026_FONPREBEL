using System.IO;
using FonPrebel.Infrastructure.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FonPrebel.Infrastructure.Logging;

/// <summary>
/// Composición única de las bitácoras (S17): todo va a <c>execution.log</c> y sólo los errores y
/// fatales van a <c>errors.log</c>, ambos por día en <c>Logs\YYYY-MM-DD\</c>.
/// </summary>
/// <remarks>
/// Nunca se registran contraseñas, credenciales ni datos sensibles (S7, S17): la bitácora recibe
/// mensajes y las cuatro propiedades de contexto, no objetos de configuración ni secretos.
/// </remarks>
public static class FonPrebelLogging
{
    /// <summary>Configuración de Serilog con los dos destinos de archivo.</summary>
    public static LoggerConfiguration CreateConfiguration(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        FonPrebelLogLineFormatter formatter = new();

        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new DailyFileSink(paths.ExecutionLogPath, formatter))
            .WriteTo.Sink(new DailyFileSink(paths.ErrorLogPath, formatter), restrictedToMinimumLevel: LogEventLevel.Error);
    }

    /// <summary>
    /// Registra las bitácoras en el contenedor del host y deja el logger global listo para los
    /// mensajes de arranque y de cierre.
    /// </summary>
    public static IServiceCollection AddFonPrebelLogging(this IServiceCollection services, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        Logger logger = CreateConfiguration(paths).CreateLogger();

        Log.Logger = logger;

        services.AddSingleton(paths);
        services.AddLogging(builder => builder.AddSerilog(logger, dispose: false));

        return services;
    }
}
