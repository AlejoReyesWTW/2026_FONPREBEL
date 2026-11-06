using System.IO;
using FonPrebel.Infrastructure.FileStorage;

namespace FonPrebel.Tests;

/// <summary>
/// Fija el requisito S5: los datos locales viven en una raíz única con <c>Database</c>,
/// <c>Logs\YYYY-MM-DD\{execution.log,errors.log}</c> y <c>Salida</c>, y las carpetas se crean al
/// consultarlas, sin rutas absolutas quemadas.
/// </summary>
public sealed class AppPathsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Root_is_the_folder_received_by_the_constructor()
    {
        AppPaths paths = new(_temp.Path);

        Assert.Equal(Path.GetFullPath(_temp.Path), paths.Root);
        Assert.True(Directory.Exists(paths.Root), "La raíz de datos debe existir al construir las rutas.");
    }

    [Fact]
    public void The_three_subfolders_hang_from_the_root()
    {
        AppPaths paths = new(_temp.Path);

        Assert.Equal(Path.Combine(paths.Root, "Database"), paths.DatabaseDirectory);
        Assert.Equal(Path.Combine(paths.Root, "Logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(paths.Root, "Salida"), paths.SalidaDirectory);
    }

    [Theory]
    [InlineData(2026, 11, 6, "2026-11-06")]
    [InlineData(2026, 1, 5, "2026-01-05")]
    [InlineData(2025, 12, 31, "2025-12-31")]
    public void The_logs_folder_of_a_day_uses_the_year_month_day_format(int year, int month, int day, string expected)
    {
        AppPaths paths = new(_temp.Path);
        DateOnly date = new(year, month, day);

        Assert.Equal(Path.Combine(paths.LogsDirectory, expected), paths.LogsDirectoryFor(date));
    }

    [Fact]
    public void The_log_file_names_are_exact()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly date = new(2026, 11, 6);
        string folder = paths.LogsDirectoryFor(date);

        Assert.Equal(Path.Combine(folder, "execution.log"), paths.ExecutionLogPath(date));
        Assert.Equal(Path.Combine(folder, "errors.log"), paths.ErrorLogPath(date));
        Assert.Equal("execution.log", Path.GetFileName(paths.ExecutionLogPath(date)));
        Assert.Equal("errors.log", Path.GetFileName(paths.ErrorLogPath(date)));
    }

    [Fact]
    public void Folders_are_created_when_they_are_queried()
    {
        AppPaths paths = new(_temp.Path);

        Assert.False(Directory.Exists(Path.Combine(paths.Root, "Database")));
        Assert.False(Directory.Exists(Path.Combine(paths.Root, "Logs")));
        Assert.False(Directory.Exists(Path.Combine(paths.Root, "Salida")));

        string database = paths.DatabaseDirectory;
        string logs = paths.LogsDirectoryFor(new DateOnly(2026, 11, 6));
        string salida = paths.SalidaDirectory;

        Assert.True(Directory.Exists(database), "Database debe crearse al consultarla.");
        Assert.True(Directory.Exists(logs), "Logs\\YYYY-MM-DD debe crearse al consultarla.");
        Assert.True(Directory.Exists(salida), "Salida debe crearse al consultarla.");
    }

    [Fact]
    public void Already_existing_folders_are_reused()
    {
        AppPaths first = new(_temp.Path);
        string database = first.DatabaseDirectory;

        AppPaths second = new(_temp.Path);

        Assert.Equal(database, second.DatabaseDirectory);
        Assert.True(Directory.Exists(database));
    }

    [Fact]
    public void Empty_roots_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new AppPaths("   "));
    }
}
