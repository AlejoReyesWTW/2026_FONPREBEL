using FonPrebel.Infrastructure.FileStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Registro de la base local en el contenedor del host (S3): una factoría de contextos de vida corta y
/// un contexto por operación, nunca compartido entre hilos.
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>Registra la factoría de contextos y los valores de base que se leen al iniciar.</summary>
    public static IServiceCollection AddFonPrebelDatabase(this IServiceCollection services, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        string connectionString = DatabaseSetup.BuildConnectionString(paths);

        services.AddSingleton<DatabaseRuntimeSettings>();
        services.AddSingleton<IDatabaseRuntimeSettings>(provider => provider.GetRequiredService<DatabaseRuntimeSettings>());

        services.AddDbContextFactory<AppDbContext>((provider, options) =>
            DatabaseSetup.ConfigureOptions(
                options,
                connectionString,
                provider.GetRequiredService<IDatabaseRuntimeSettings>()));

        services.AddSingleton<DatabaseStartupService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<DatabaseStartupService>());

        return services;
    }
}
