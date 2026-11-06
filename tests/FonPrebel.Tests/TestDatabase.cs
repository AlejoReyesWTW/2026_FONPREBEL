using System.IO;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Tests;

/// <summary>
/// Base SQLite en un archivo temporal, armada con el mismo registro que usa la aplicación. Ninguna
/// prueba toca <c>%LOCALAPPDATA%</c> real.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _provider;

    public TestDatabase()
    {
        Paths = new AppPaths(Path.Combine(_temp.Path, "data"));

        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFonPrebelDatabase(Paths);

        _provider = services.BuildServiceProvider();
        Factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        Settings = _provider.GetRequiredService<DatabaseRuntimeSettings>();
    }

    public AppPaths Paths { get; }

    public IDbContextFactory<AppDbContext> Factory { get; }

    public DatabaseRuntimeSettings Settings { get; }

    public IServiceProvider Services => _provider;

    public string DatabasePath => System.IO.Path.Combine(Paths.DatabaseDirectory, DatabaseSetup.DatabaseFileName);

    public AppDbContext CreateContext() => Factory.CreateDbContext();

    /// <summary>Aplica las migraciones pendientes, una vez por llamada, con un contexto propio.</summary>
    public void Migrate()
    {
        using AppDbContext context = CreateContext();
        context.Database.Migrate();
    }

    public void Dispose()
    {
        _provider.Dispose();

        // La agrupación de conexiones de SQLite conserva el archivo abierto y la carpeta temporal no
        // se puede borrar mientras tanto.
        SqliteConnection.ClearAllPools();
        _temp.Dispose();
    }
}
