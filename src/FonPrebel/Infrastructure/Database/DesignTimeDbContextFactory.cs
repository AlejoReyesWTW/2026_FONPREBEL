using FonPrebel.Infrastructure.FileStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Sólo para las herramientas de EF (<c>dotnet ef migrations</c>): arma el contexto con la ruta real de
/// datos locales y los valores por omisión, sin arrancar la aplicación. Los comandos de migración no
/// abren la base, así que no se crea ni se modifica ningún archivo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppPaths paths = new();
        DbContextOptionsBuilder<AppDbContext> options = new();

        DatabaseSetup.ConfigureOptions(options, DatabaseSetup.BuildConnectionString(paths), new DatabaseRuntimeSettings());

        return new AppDbContext(options.Options);
    }
}
