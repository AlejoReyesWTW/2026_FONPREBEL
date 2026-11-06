using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Registro de la configuración administrada desde la pantalla (S6): el almacén clave/valor, la fila
/// única de correo y el estado compartido que gobierna el botón INICIAR (S18).
/// </summary>
/// <remarks>
/// Los tres son sin estado entre operaciones —cada llamada abre su propio contexto de base— y viven
/// como singletons del host, igual que el resto de la infraestructura.
/// </remarks>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>Registra los almacenes de configuración y el gate de INICIAR.</summary>
    public static IServiceCollection AddFonPrebelConfiguration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAppConfigurationStore, AppConfigurationStore>();
        services.AddSingleton<IEmailSettingsStore, EmailSettingsStore>();
        services.AddSingleton<ConfigurationReadinessGate>();

        return services;
    }
}
