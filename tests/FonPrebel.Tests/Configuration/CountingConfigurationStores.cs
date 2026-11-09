using FonPrebel.Application.Interfaces;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Almacén de configuración que delega en el real y cuenta las escrituras (§2.9.2 r11): así la prueba
/// observa si la pantalla escribió o no, sin mirar las tablas por dentro.
/// </summary>
internal sealed class CountingConfigurationStore : IAppConfigurationStore
{
    private readonly IAppConfigurationStore _inner;

    public CountingConfigurationStore(IAppConfigurationStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <summary>Cantidad de escrituras pedidas al almacén.</summary>
    public int WriteCount { get; private set; }

    public Task<ConfigurationReadResult<string>> ReadTextAsync(
        string key,
        CancellationToken cancellationToken = default) => _inner.ReadTextAsync(key, cancellationToken);

    public Task<ConfigurationIntResult> ReadIntAsync(
        string key,
        CancellationToken cancellationToken = default) => _inner.ReadIntAsync(key, cancellationToken);

    public Task<ConfigurationBoolResult> ReadBoolAsync(
        string key,
        CancellationToken cancellationToken = default) => _inner.ReadBoolAsync(key, cancellationToken);

    public Task<ConfigurationDurationResult> ReadDurationAsync(
        string key,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default) =>
        _inner.ReadDurationAsync(key, unit, cancellationToken);

    public Task<ConfigurationWriteResult> WriteTextAsync(
        string key,
        string? value,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;

        return _inner.WriteTextAsync(key, value, cancellationToken);
    }

    public Task<ConfigurationWriteResult> WriteIntAsync(
        string key,
        int? value,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;

        return _inner.WriteIntAsync(key, value, cancellationToken);
    }

    public Task<ConfigurationWriteResult> WriteBoolAsync(
        string key,
        bool? value,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;

        return _inner.WriteBoolAsync(key, value, cancellationToken);
    }

    public Task<ConfigurationWriteResult> WriteDurationAsync(
        string key,
        TimeSpan? value,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;

        return _inner.WriteDurationAsync(key, value, unit, cancellationToken);
    }

    /// <summary>La siembra inicial no cuenta como guardado del analista.</summary>
    public Task<ConfigurationWriteResult> EnsureDefaultsAsync(CancellationToken cancellationToken = default) =>
        _inner.EnsureDefaultsAsync(cancellationToken);
}

/// <summary>
/// Almacén de correo que delega en el real y cuenta las escrituras, con el mismo propósito que
/// <see cref="CountingConfigurationStore"/>.
/// </summary>
internal sealed class CountingEmailSettingsStore : IEmailSettingsStore
{
    private readonly IEmailSettingsStore _inner;

    public CountingEmailSettingsStore(IEmailSettingsStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <summary>Cantidad de escrituras pedidas al almacén.</summary>
    public int WriteCount { get; private set; }

    public Task<ConfigurationReadResult<EmailSettings>> ReadAsync(CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(cancellationToken);

    public Task<ConfigurationWriteResult> WriteAsync(
        EmailSettings settings,
        CancellationToken cancellationToken = default)
    {
        WriteCount++;

        return _inner.WriteAsync(settings, cancellationToken);
    }
}
