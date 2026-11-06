using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S6: la siembra de <c>AppConfiguration</c> registra todas las claves del diseño de datos y da valor
/// sólo a las dos que el spec confirma, sin inventar ninguno y sin sobreescribir lo ya guardado.
/// </summary>
public sealed class AppConfigurationStoreTests
{
    private static readonly string[] ExpectedKeys =
    {
        "FileServer.Route1",
        "FileServer.Route2",
        "FileServer.Route3",
        "FileServer.Route4",
        "Output.Folder",
        "Connections.AutoCheckMinutes",
        "Connections.SlowThresholdMs",
        "Mail.ProxyEnabled",
        "Mail.ProxyText",
        "Mail.SendTimeoutSeconds",
        "Database.BusyTimeoutMs",
        "Database.WalEnabled",
        "Workers.PdfCount",
        "Workers.EmailCount",
    };

    [Fact]
    public async Task EnsureDefaults_seeds_only_the_two_confirmed_values()
    {
        using TestDatabase database = new();
        database.Migrate();
        AppConfigurationStore store = CreateStore(database);

        ConfigurationWriteResult result = await store.EnsureDefaultsAsync();

        Assert.True(result.Succeeded);

        Dictionary<string, string?> rows = ReadRows(database);

        Assert.Equal(ExpectedKeys.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("2", rows["Connections.AutoCheckMinutes"]);
        Assert.Equal("true", rows["Database.WalEnabled"]);

        string?[] pending = rows
            .Where(row => row.Key is not ("Connections.AutoCheckMinutes" or "Database.WalEnabled"))
            .Select(row => row.Value)
            .ToArray();

        Assert.All(pending, value => Assert.Null(value));
    }

    [Fact]
    public async Task EnsureDefaults_is_idempotent_and_keeps_a_value_the_analyst_changed()
    {
        using TestDatabase database = new();
        database.Migrate();
        AppConfigurationStore store = CreateStore(database);

        await store.EnsureDefaultsAsync();
        await store.WriteIntAsync(AppConfigurationKeys.AutoCheckMinutes, 5);
        await store.EnsureDefaultsAsync();

        Dictionary<string, string?> rows = ReadRows(database);

        Assert.Equal(ExpectedKeys.Length, rows.Count);
        Assert.Equal("5", rows["Connections.AutoCheckMinutes"]);
        Assert.Equal("true", rows["Database.WalEnabled"]);
    }

    [Fact]
    public async Task Read_and_write_are_typed_by_key()
    {
        using TestDatabase database = new();
        database.Migrate();
        AppConfigurationStore store = CreateStore(database);

        await store.WriteIntAsync(AppConfigurationKeys.SlowThresholdMs, 1500);
        await store.WriteBoolAsync(AppConfigurationKeys.ProxyEnabled, true);
        await store.WriteDurationAsync(AppConfigurationKeys.AutoCheckMinutes, TimeSpan.FromMinutes(2), ConfigurationTimeUnit.Minutes);
        await store.WriteDurationAsync(AppConfigurationKeys.SendTimeoutSeconds, TimeSpan.FromSeconds(45), ConfigurationTimeUnit.Seconds);

        Assert.Equal(1500, (await store.ReadIntAsync(AppConfigurationKeys.SlowThresholdMs)).Value);
        Assert.True((await store.ReadBoolAsync(AppConfigurationKeys.ProxyEnabled)).Value);
        Assert.Equal(
            TimeSpan.FromMinutes(2),
            (await store.ReadDurationAsync(AppConfigurationKeys.AutoCheckMinutes, ConfigurationTimeUnit.Minutes)).Value);
        Assert.Equal(
            TimeSpan.FromSeconds(45),
            (await store.ReadDurationAsync(AppConfigurationKeys.SendTimeoutSeconds, ConfigurationTimeUnit.Seconds)).Value);
    }

    [Fact]
    public async Task A_pending_parameter_reads_as_null_and_blank_text_is_stored_as_null()
    {
        using TestDatabase database = new();
        database.Migrate();
        AppConfigurationStore store = CreateStore(database);

        await store.WriteTextAsync(AppConfigurationKeys.ProxyText, "   ");

        ConfigurationReadResult<string> text = await store.ReadTextAsync(AppConfigurationKeys.ProxyText);

        Assert.True(text.Succeeded);
        Assert.Null(text.Value);

        ConfigurationIntResult number = await store.ReadIntAsync(AppConfigurationKeys.PdfWorkers);

        Assert.True(number.Succeeded);
        Assert.Null(number.Value);
    }

    [Fact]
    public async Task An_unregistered_key_is_rejected()
    {
        using TestDatabase database = new();
        database.Migrate();
        AppConfigurationStore store = CreateStore(database);

        ConfigurationWriteResult write = await store.WriteTextAsync("Parametro.Inventado", "x");
        ConfigurationReadResult<string> read = await store.ReadTextAsync("Parametro.Inventado");

        Assert.Equal(ConfigurationStoreOutcome.KeyNotRegistered, write.Outcome);
        Assert.Equal(ConfigurationStoreOutcome.KeyNotRegistered, read.Outcome);
        Assert.Empty(ReadRows(database));
    }

    [Fact]
    public async Task An_inaccessible_database_returns_a_typed_failure()
    {
        AppConfigurationStore store = new(new ThrowingContextFactory(), NullLogger<AppConfigurationStore>.Instance);

        ConfigurationReadResult<string> read = await store.ReadTextAsync(AppConfigurationKeys.Route1);
        ConfigurationWriteResult write = await store.WriteTextAsync(AppConfigurationKeys.Route1, "x");
        ConfigurationWriteResult seed = await store.EnsureDefaultsAsync();

        Assert.Equal(ConfigurationStoreOutcome.StorageUnavailable, read.Outcome);
        Assert.Equal(ConfigurationStoreOutcome.StorageUnavailable, write.Outcome);
        Assert.Equal(ConfigurationStoreOutcome.StorageUnavailable, seed.Outcome);
    }

    [Fact]
    public void The_registered_keys_match_the_database_and_startup_keys()
    {
        Assert.Equal(AppConfigurationKeys.BusyTimeoutMs, DatabaseSetup.BusyTimeoutKey);
        Assert.Equal(AppConfigurationKeys.WalEnabled, DatabaseSetup.WalEnabledKey);
        Assert.Equal(ExpectedKeys.Length, AppConfigurationKeys.Registered.Count);
        Assert.All(AppConfigurationKeys.Registered, key => Assert.Contains(key, ExpectedKeys));
    }

    private static AppConfigurationStore CreateStore(TestDatabase database) =>
        new(database.Factory, NullLogger<AppConfigurationStore>.Instance);

    private static Dictionary<string, string?> ReadRows(TestDatabase database)
    {
        using AppDbContext context = database.CreateContext();

        return context.AppConfigurations
            .AsNoTracking()
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }
}
