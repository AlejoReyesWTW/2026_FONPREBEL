namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Resultado de una operación contra el almacén de configuración. Una base inaccesible se informa acá
/// y no como excepción, para que la pantalla muestre el aviso en vez de dejar caer la aplicación
/// (obligación heredada de A3 y A4).
/// </summary>
public enum ConfigurationStoreOutcome
{
    /// <summary>La operación se completó.</summary>
    Succeeded = 0,

    /// <summary>La clave no está registrada en el diseño de datos: no se lee ni se escribe.</summary>
    KeyNotRegistered = 1,

    /// <summary>El valor recibido no corresponde al tipo pedido.</summary>
    InvalidValue = 2,

    /// <summary>El valor guardado no tiene el formato esperado para el tipo pedido.</summary>
    InvalidStoredValue = 3,

    /// <summary>La base local no está accesible o no está migrada; no se modificó nada.</summary>
    StorageUnavailable = 4,
}

/// <summary>Resultado de una escritura en el almacén de configuración.</summary>
public sealed record ConfigurationWriteResult(ConfigurationStoreOutcome Outcome)
{
    /// <summary>Resultado exitoso.</summary>
    public static ConfigurationWriteResult Success { get; } = new(ConfigurationStoreOutcome.Succeeded);

    /// <summary>Indica si la escritura se completó.</summary>
    public bool Succeeded => Outcome == ConfigurationStoreOutcome.Succeeded;

    /// <summary>Resultado fallido con su motivo.</summary>
    public static ConfigurationWriteResult Failed(ConfigurationStoreOutcome outcome)
    {
        if (outcome == ConfigurationStoreOutcome.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.");
        }

        return new ConfigurationWriteResult(outcome);
    }
}

/// <summary>Resultado de lectura de un valor de tipo <typeparamref name="T"/>.</summary>
/// <typeparam name="T">Tipo del valor; <c>null</c> significa que el parámetro sigue pendiente.</typeparam>
public sealed class ConfigurationReadResult<T>
    where T : class
{
    private ConfigurationReadResult(ConfigurationStoreOutcome outcome, T? value)
    {
        Outcome = outcome;
        Value = value;
    }

    /// <summary>Motivo del resultado.</summary>
    public ConfigurationStoreOutcome Outcome { get; }

    /// <summary>Valor leído; nulo cuando el parámetro está pendiente.</summary>
    public T? Value { get; }

    /// <summary>Indica si la lectura se completó.</summary>
    public bool Succeeded => Outcome == ConfigurationStoreOutcome.Succeeded;

    /// <summary>Indica si la lectura se completó y hay un valor configurado.</summary>
    public bool HasValue => Succeeded && Value is not null;

    /// <summary>Resultado con el valor leído.</summary>
    public static ConfigurationReadResult<T> Ok(T? value) => new(ConfigurationStoreOutcome.Succeeded, value);

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static ConfigurationReadResult<T> Failed(ConfigurationStoreOutcome outcome)
    {
        if (outcome == ConfigurationStoreOutcome.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.");
        }

        return new ConfigurationReadResult<T>(outcome, null);
    }
}

/// <summary>Resultado de lectura de un entero; <c>Value</c> nulo significa "pendiente".</summary>
public sealed class ConfigurationIntResult
{
    private ConfigurationIntResult(ConfigurationStoreOutcome outcome, int? value)
    {
        Outcome = outcome;
        Value = value;
    }

    /// <summary>Motivo del resultado.</summary>
    public ConfigurationStoreOutcome Outcome { get; }

    /// <summary>Valor leído; nulo cuando el parámetro está pendiente.</summary>
    public int? Value { get; }

    /// <summary>Indica si la lectura se completó.</summary>
    public bool Succeeded => Outcome == ConfigurationStoreOutcome.Succeeded;

    /// <summary>Resultado con el valor leído.</summary>
    public static ConfigurationIntResult Ok(int? value) => new(ConfigurationStoreOutcome.Succeeded, value);

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static ConfigurationIntResult Failed(ConfigurationStoreOutcome outcome) =>
        new(RequireFailure(outcome), null);

    private static ConfigurationStoreOutcome RequireFailure(ConfigurationStoreOutcome outcome) =>
        outcome == ConfigurationStoreOutcome.Succeeded
            ? throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.")
            : outcome;
}

/// <summary>Resultado de lectura de un booleano; <c>Value</c> nulo significa "pendiente".</summary>
public sealed class ConfigurationBoolResult
{
    private ConfigurationBoolResult(ConfigurationStoreOutcome outcome, bool? value)
    {
        Outcome = outcome;
        Value = value;
    }

    /// <summary>Motivo del resultado.</summary>
    public ConfigurationStoreOutcome Outcome { get; }

    /// <summary>Valor leído; nulo cuando el parámetro está pendiente.</summary>
    public bool? Value { get; }

    /// <summary>Indica si la lectura se completó.</summary>
    public bool Succeeded => Outcome == ConfigurationStoreOutcome.Succeeded;

    /// <summary>Resultado con el valor leído.</summary>
    public static ConfigurationBoolResult Ok(bool? value) => new(ConfigurationStoreOutcome.Succeeded, value);

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static ConfigurationBoolResult Failed(ConfigurationStoreOutcome outcome) =>
        new(RequireFailure(outcome), null);

    private static ConfigurationStoreOutcome RequireFailure(ConfigurationStoreOutcome outcome) =>
        outcome == ConfigurationStoreOutcome.Succeeded
            ? throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.")
            : outcome;
}

/// <summary>Resultado de lectura de un intervalo; <c>Value</c> nulo significa "pendiente".</summary>
public sealed class ConfigurationDurationResult
{
    private ConfigurationDurationResult(ConfigurationStoreOutcome outcome, TimeSpan? value)
    {
        Outcome = outcome;
        Value = value;
    }

    /// <summary>Motivo del resultado.</summary>
    public ConfigurationStoreOutcome Outcome { get; }

    /// <summary>Valor leído; nulo cuando el parámetro está pendiente.</summary>
    public TimeSpan? Value { get; }

    /// <summary>Indica si la lectura se completó.</summary>
    public bool Succeeded => Outcome == ConfigurationStoreOutcome.Succeeded;

    /// <summary>Resultado con el valor leído.</summary>
    public static ConfigurationDurationResult Ok(TimeSpan? value) =>
        new(ConfigurationStoreOutcome.Succeeded, value);

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static ConfigurationDurationResult Failed(ConfigurationStoreOutcome outcome) =>
        new(RequireFailure(outcome), null);

    private static ConfigurationStoreOutcome RequireFailure(ConfigurationStoreOutcome outcome) =>
        outcome == ConfigurationStoreOutcome.Succeeded
            ? throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.")
            : outcome;
}

/// <summary>
/// Unidad con la que se guarda un intervalo en <c>AppConfiguration</c>: el modelo clave/valor guarda
/// texto, así que la unidad la fija la clave (minutos para la verificación, segundos para el envío).
/// </summary>
public enum ConfigurationTimeUnit
{
    /// <summary>El valor se guarda como cantidad de segundos.</summary>
    Seconds = 0,

    /// <summary>El valor se guarda como cantidad de minutos.</summary>
    Minutes = 1,
}
