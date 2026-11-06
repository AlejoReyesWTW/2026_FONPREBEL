using System.Globalization;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// S6 (§2.9): almacén de los parámetros generales en <c>AppConfiguration</c>. Un contexto por operación
/// desde la factoría, sin SQL en texto y sin valores por defecto inventados: sólo se siembran los dos
/// valores que el spec confirma.
/// </summary>
/// <remarks>
/// Una base inaccesible o sin migrar se traduce a
/// <see cref="ConfigurationStoreOutcome.StorageUnavailable"/> y nunca a una excepción que llegue a la
/// interfaz. Los valores se guardan siempre con cultura invariante.
/// </remarks>
public sealed class AppConfigurationStore : IAppConfigurationStore
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<AppConfigurationStore> _logger;

    public AppConfigurationStore(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<AppConfigurationStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ConfigurationReadResult<string>> ReadTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (!AppConfigurationKeys.IsRegistered(key))
        {
            return ConfigurationReadResult<string>.Failed(ConfigurationStoreOutcome.KeyNotRegistered);
        }

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            string? value = await context.AppConfigurations
                .AsNoTracking()
                .Where(configuration => configuration.Key == key)
                .Select(configuration => configuration.Value)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return ConfigurationReadResult<string>.Ok(value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "leer", key);

            return ConfigurationReadResult<string>.Failed(ConfigurationStoreOutcome.StorageUnavailable);
        }
    }

    public async Task<ConfigurationIntResult> ReadIntAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ConfigurationReadResult<string> text = await ReadTextAsync(key, cancellationToken).ConfigureAwait(false);
        if (!text.Succeeded)
        {
            return ConfigurationIntResult.Failed(text.Outcome);
        }

        if (text.Value is null)
        {
            return ConfigurationIntResult.Ok(null);
        }

        return TryParseInt(text.Value, out int number)
            ? ConfigurationIntResult.Ok(number)
            : ConfigurationIntResult.Failed(ConfigurationStoreOutcome.InvalidStoredValue);
    }

    public async Task<ConfigurationBoolResult> ReadBoolAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ConfigurationReadResult<string> text = await ReadTextAsync(key, cancellationToken).ConfigureAwait(false);
        if (!text.Succeeded)
        {
            return ConfigurationBoolResult.Failed(text.Outcome);
        }

        if (text.Value is null)
        {
            return ConfigurationBoolResult.Ok(null);
        }

        return bool.TryParse(text.Value, out bool flag)
            ? ConfigurationBoolResult.Ok(flag)
            : ConfigurationBoolResult.Failed(ConfigurationStoreOutcome.InvalidStoredValue);
    }

    public async Task<ConfigurationDurationResult> ReadDurationAsync(
        string key,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default)
    {
        ConfigurationIntResult number = await ReadIntAsync(key, cancellationToken).ConfigureAwait(false);
        if (!number.Succeeded)
        {
            return ConfigurationDurationResult.Failed(number.Outcome);
        }

        if (number.Value is not int amount)
        {
            return ConfigurationDurationResult.Ok(null);
        }

        TimeSpan value = unit == ConfigurationTimeUnit.Minutes
            ? TimeSpan.FromMinutes(amount)
            : TimeSpan.FromSeconds(amount);

        return ConfigurationDurationResult.Ok(value);
    }

    public Task<ConfigurationWriteResult> WriteTextAsync(
        string key,
        string? value,
        CancellationToken cancellationToken = default) =>
        SaveAsync(key, Normalize(value), cancellationToken);

    public Task<ConfigurationWriteResult> WriteIntAsync(
        string key,
        int? value,
        CancellationToken cancellationToken = default) =>
        SaveAsync(key, value?.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<ConfigurationWriteResult> WriteBoolAsync(
        string key,
        bool? value,
        CancellationToken cancellationToken = default) =>
        SaveAsync(key, value is bool flag ? (flag ? "true" : "false") : null, cancellationToken);

    public Task<ConfigurationWriteResult> WriteDurationAsync(
        string key,
        TimeSpan? value,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default)
    {
        if (value is not TimeSpan duration)
        {
            return SaveAsync(key, null, cancellationToken);
        }

        double amount = unit == ConfigurationTimeUnit.Minutes
            ? Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero)
            : Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);

        if (amount < int.MinValue || amount > int.MaxValue)
        {
            return Task.FromResult(ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.InvalidValue));
        }

        return SaveAsync(key, ((int)amount).ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task<ConfigurationWriteResult> EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            HashSet<string> existing = (await context.AppConfigurations
                    .AsNoTracking()
                    .Select(configuration => configuration.Key)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);

            DateTime now = DateTime.UtcNow;
            List<AppConfiguration> missing = new();

            foreach (string key in AppConfigurationKeys.Registered)
            {
                if (existing.Contains(key))
                {
                    continue;
                }

                missing.Add(new AppConfiguration
                {
                    Key = key,
                    Value = AppConfigurationKeys.ConfirmedInitialValue(key),
                    UpdatedAtUtc = now,
                });
            }

            if (missing.Count > 0)
            {
                context.AppConfigurations.AddRange(missing);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Parámetros de configuración registrados: {Added} nuevos de {Total} claves.",
                missing.Count,
                AppConfigurationKeys.Registered.Count);

            return ConfigurationWriteResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "sembrar", "los parámetros generales");

            return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.StorageUnavailable);
        }
    }

    /// <summary>Un valor vacío o en blanco significa "pendiente de configurar" y se guarda nulo.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseInt(string value, out int number) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

    /// <summary>Escribe una clave existente o crea su fila; es la única puerta de escritura del almacén.</summary>
    private async Task<ConfigurationWriteResult> SaveAsync(
        string key,
        string? value,
        CancellationToken cancellationToken)
    {
        if (!AppConfigurationKeys.IsRegistered(key))
        {
            return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.KeyNotRegistered);
        }

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            AppConfiguration? row = await context.AppConfigurations
                .SingleOrDefaultAsync(configuration => configuration.Key == key, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                row = new AppConfiguration { Key = key };
                context.AppConfigurations.Add(row);
            }

            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ConfigurationWriteResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStorageFailure(exception, "guardar", key);

            return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.StorageUnavailable);
        }
    }

    private void LogStorageFailure(Exception exception, string action, string key) =>
        _logger.LogError(
            exception,
            "No se pudo {Action} el parámetro {Key}: la base local no está accesible o no está migrada. No se modificó nada.",
            action,
            key);
}

/// <summary>
/// S6: almacén de la fila única de <c>EmailConfiguration</c>. Sólo toca los parámetros de correo: el
/// secreto cifrado y su fecha quedan intactos, porque los administra <c>IEmailCredentialService</c>.
/// </summary>
public sealed class EmailSettingsStore : IEmailSettingsStore
{
    private const int SingleRowId = 1;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<EmailSettingsStore> _logger;

    public EmailSettingsStore(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<EmailSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ConfigurationReadResult<EmailSettings>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // Se proyectan sólo los parámetros de correo: el secreto cifrado no se lee en esta pantalla.
            EmailSettings? settings = await context.EmailConfigurations
                .AsNoTracking()
                .Where(configuration => configuration.Id == SingleRowId)
                .Select(configuration => new EmailSettings(
                    configuration.Host,
                    configuration.Port,
                    configuration.Security,
                    configuration.Account,
                    configuration.SenderAddress))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return ConfigurationReadResult<EmailSettings>.Ok(settings ?? EmailSettings.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "No se pudo leer la configuración de correo: la base local no está accesible o no está migrada.");

            return ConfigurationReadResult<EmailSettings>.Failed(ConfigurationStoreOutcome.StorageUnavailable);
        }
    }

    public async Task<ConfigurationWriteResult> WriteAsync(
        EmailSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await using AppDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            EmailConfiguration? configuration = await context.EmailConfigurations
                .SingleOrDefaultAsync(row => row.Id == SingleRowId, cancellationToken)
                .ConfigureAwait(false);

            if (configuration is null)
            {
                configuration = new EmailConfiguration { Id = SingleRowId };
                context.EmailConfigurations.Add(configuration);
            }

            configuration.Host = Normalize(settings.Host);
            configuration.Port = settings.Port;
            configuration.Security = Normalize(settings.Security);
            configuration.Account = Normalize(settings.Account);
            configuration.SenderAddress = Normalize(settings.SenderAddress);
            configuration.UpdatedAtUtc = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ConfigurationWriteResult.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "No se pudo guardar la configuración de correo: la base local no está accesible o no está migrada. No se modificó nada.");

            return ConfigurationWriteResult.Failed(ConfigurationStoreOutcome.StorageUnavailable);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
