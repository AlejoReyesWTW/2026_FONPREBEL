namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Valores vivos de <see cref="IDatabaseRuntimeSettings"/>. El interceptor de conexiones lo recibe como
/// singleton mutable: el servicio de arranque los completa después de aplicar las migraciones, así que
/// una conexión abierta antes de esa lectura usa el valor por omisión.
/// </summary>
public sealed class DatabaseRuntimeSettings : IDatabaseRuntimeSettings
{
    /// <summary>
    /// Valor con el que arranca <c>Database.WalEnabled</c> (§3.6 del diseño): el spec ordena activar
    /// WAL y lo que queda pendiente es el tiempo de espera.
    /// </summary>
    public const bool DefaultWalEnabled = true;

    /// <inheritdoc />
    public bool WalEnabled { get; set; } = DefaultWalEnabled;

    /// <inheritdoc />
    public int? BusyTimeoutMs { get; set; }
}
