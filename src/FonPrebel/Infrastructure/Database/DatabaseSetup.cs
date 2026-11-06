using System.IO;
using FonPrebel.Infrastructure.FileStorage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Arma la cadena de conexión y las opciones del contexto local (S3). Es el único lugar donde se
/// decide cómo se abre <c>FonPrebel.db</c>, de modo que la aplicación y las pruebas usan la misma
/// composición.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>Nombre exacto del archivo de base local dentro de <c>Database/</c> (S5).</summary>
    public const string DatabaseFileName = "FonPrebel.db";

    /// <summary>Clave de <c>AppConfiguration</c> del tiempo de espera ante bloqueo (§3.6, pendiente).</summary>
    public const string BusyTimeoutKey = "Database.BusyTimeoutMs";

    /// <summary>Clave de <c>AppConfiguration</c> que habilita el modo WAL (§3.6, arranca en <c>true</c>).</summary>
    public const string WalEnabledKey = "Database.WalEnabled";

    /// <summary>Cadena de conexión al archivo de base de la raíz de datos locales indicada.</summary>
    public static string BuildConnectionString(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return BuildConnectionString(paths.DatabaseDirectory);
    }

    /// <summary>Cadena de conexión al archivo de base dentro de la carpeta indicada.</summary>
    public static string BuildConnectionString(string databaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseDirectory);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(databaseDirectory, DatabaseFileName),
            ForeignKeys = true,
        }.ToString();
    }

    /// <summary>
    /// Opciones del contexto: proveedor SQLite sobre la cadena indicada, con las conexiones pasando por
    /// el interceptor que aplica WAL y el tiempo de espera.
    /// </summary>
    public static void ConfigureOptions(
        DbContextOptionsBuilder options,
        string connectionString,
        IDatabaseRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.UseSqlite(connectionString);
        options.AddInterceptors(new SqlitePragmaInterceptor(settings));
    }
}
