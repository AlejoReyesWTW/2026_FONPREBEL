using System.Globalization;
using System.IO;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace FonPrebel.Tests;

/// <summary>
/// S3: el arranque aplica las migraciones una sola vez, registra el fallo en <c>errors.log</c> sin tocar
/// la base y deja el tiempo de espera pendiente en lugar de inventarlo.
/// </summary>
public sealed class DatabaseStartupServiceTests
{
    private static readonly DateTime SampleUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Failing_migrations_are_logged_to_errors_log_and_the_file_is_kept()
    {
        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);
        string databasePath = Path.Combine(paths.DatabaseDirectory, DatabaseSetup.DatabaseFileName);
        byte[] original = "esto no es una base SQLite"u8.ToArray();
        File.WriteAllBytes(databasePath, original);

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        using (DatabaseHarness harness = new(paths, logger))
        {
            Assert.False(harness.Startup.Apply());
            Assert.NotNull(harness.Startup.Failure);
        }

        Assert.Contains(
            "No se pudieron aplicar las migraciones",
            File.ReadAllText(paths.ErrorLogPath(today)));
        Assert.Equal(original, File.ReadAllBytes(databasePath));
    }

    [Fact]
    public void Pending_busy_timeout_is_reported_as_pending_and_not_invented()
    {
        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        using (DatabaseHarness harness = new(paths, logger))
        {
            Assert.True(harness.Startup.Apply());
            Assert.Null(harness.Settings.BusyTimeoutMs);
        }

        string executionLog = File.ReadAllText(paths.ExecutionLogPath(today));

        Assert.Contains(DatabaseSetup.BusyTimeoutKey, executionLog);
        Assert.Contains("está pendiente de configurar", executionLog);
    }

    [Fact]
    public void Configured_busy_timeout_is_applied_to_the_connections()
    {
        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        using (DatabaseHarness harness = new(paths, logger))
        {
            Assert.True(harness.Startup.Apply());

            using (AppDbContext context = harness.ContextFactory.CreateDbContext())
            {
                context.AppConfigurations.Add(new AppConfiguration
                {
                    Key = DatabaseSetup.BusyTimeoutKey,
                    Value = "700",
                    UpdatedAtUtc = SampleUtc,
                });
                context.SaveChanges();
            }

            Assert.True(harness.Startup.Apply());
            Assert.Equal(700, harness.Settings.BusyTimeoutMs);

            using AppDbContext reloaded = harness.ContextFactory.CreateDbContext();
            reloaded.Database.OpenConnection();
            using System.Data.Common.DbCommand command = reloaded.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA busy_timeout;";

            Assert.Equal(700L, Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Composición mínima: la factoría real y los servicios que usa el arranque.</summary>
    private sealed class DatabaseHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public DatabaseHarness(AppPaths paths, Logger logger)
        {
            ServiceCollection services = new();
            services.AddLogging(builder => builder.AddSerilog(logger, dispose: false));
            services.AddFonPrebelDatabase(paths);

            _provider = services.BuildServiceProvider();
            ContextFactory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            Settings = _provider.GetRequiredService<DatabaseRuntimeSettings>();
            Startup = _provider.GetRequiredService<DatabaseStartupService>();
        }

        public IDbContextFactory<AppDbContext> ContextFactory { get; }

        public DatabaseRuntimeSettings Settings { get; }

        public DatabaseStartupService Startup { get; }

        public void Dispose()
        {
            _provider.Dispose();
            SqliteConnection.ClearAllPools();
        }
    }
}
