namespace FonPrebel.Application.Interfaces;

/// <summary>
/// S6 (§2.9): lectura y escritura de los parámetros generales guardados en SQLite, por clave tipada y
/// sobre las claves registradas en el diseño de datos. No hay archivo <c>.env</c> ni valores por
/// defecto inventados: un parámetro sin valor confirmado se guarda nulo y la pantalla lo muestra vacío.
/// </summary>
/// <remarks>
/// Cada operación abre su propio contexto desde la factoría (S3). Una base inaccesible o no migrada se
/// traduce a <see cref="ConfigurationStoreOutcome.StorageUnavailable"/> y nunca a una excepción hacia
/// la interfaz de usuario.
/// </remarks>
public interface IAppConfigurationStore
{
    /// <summary>Lee el valor de texto de una clave; <c>null</c> significa "pendiente de configurar".</summary>
    Task<ConfigurationReadResult<string>> ReadTextAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lee el valor entero de una clave; <c>null</c> significa "pendiente de configurar".</summary>
    Task<ConfigurationIntResult> ReadIntAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lee el valor booleano de una clave; <c>null</c> significa "pendiente de configurar".</summary>
    Task<ConfigurationBoolResult> ReadBoolAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lee un intervalo guardado en la unidad que la clave fija, por ejemplo minutos para la
    /// verificación automática y segundos para el tiempo de espera por envío.
    /// </summary>
    Task<ConfigurationDurationResult> ReadDurationAsync(
        string key,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda texto en una clave. Un valor vacío o en blanco se guarda como nulo, que es "pendiente de
    /// configurar" y nunca un valor inventado.
    /// </summary>
    Task<ConfigurationWriteResult> WriteTextAsync(
        string key,
        string? value,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda un entero o, con <c>null</c>, deja el parámetro pendiente.</summary>
    Task<ConfigurationWriteResult> WriteIntAsync(
        string key,
        int? value,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda un booleano o, con <c>null</c>, deja el parámetro pendiente.</summary>
    Task<ConfigurationWriteResult> WriteBoolAsync(
        string key,
        bool? value,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda un intervalo en la unidad que la clave fija o, con <c>null</c>, lo deja pendiente.</summary>
    Task<ConfigurationWriteResult> WriteDurationAsync(
        string key,
        TimeSpan? value,
        ConfigurationTimeUnit unit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Siembra idempotente: registra todas las claves del diseño de datos y les da valor sólo a las dos
    /// que el spec confirma (<c>Connections.AutoCheckMinutes = 2</c> y <c>Database.WalEnabled = true</c>).
    /// Las claves que ya tienen fila no se tocan: correrlo dos veces no duplica ni sobreescribe.
    /// </summary>
    Task<ConfigurationWriteResult> EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
