namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Resultado de una operación sobre lotes, ejecuciones, destinatarios o huellas de entrada (§2.8, Anexo F).
/// Una base inaccesible o sin migrar se informa acá y nunca como excepción hacia la interfaz de usuario
/// (obligación heredada de A3 y A4).
/// </summary>
public enum BatchOutcome
{
    /// <summary>La operación se completó.</summary>
    Succeeded = 0,

    /// <summary>No existe el lote pedido.</summary>
    BatchNotFound = 1,

    /// <summary>No existe la ejecución pedida.</summary>
    ExecutionNotFound = 2,

    /// <summary>El identificador de lote generado ya está usado: no se duplica (§2.8 regla 1).</summary>
    DuplicateCode = 3,

    /// <summary>El jubilado ya está registrado en el lote: la clave <c>(BatchId, Cedula)</c> es única (§3.4).</summary>
    DuplicateRecipient = 4,

    /// <summary>El destinatario no existe dentro del lote.</summary>
    RecipientNotFound = 5,

    /// <summary>El periodo del lote no es válido: mes de 1 a 12 y año de cuatro dígitos.</summary>
    InvalidPeriod = 6,

    /// <summary>La operación no corresponde al estado actual del lote o de la ejecución (Anexo F.8).</summary>
    InvalidExecutionState = 7,

    /// <summary>Los datos recibidos no alcanzan para la operación pedida.</summary>
    InvalidData = 8,

    /// <summary>
    /// Se pidió marcar un correo como enviado sin la confirmación del mecanismo de envío: la aplicación no
    /// lo afirma (§2.8 regla 12).
    /// </summary>
    ConfirmationRequired = 9,

    /// <summary>Un archivo de entrada no está disponible o no se puede leer (Anexo F.4 y F.5).</summary>
    InputNotAvailable = 10,

    /// <summary>La base local no está accesible o no está migrada; no se modificó nada.</summary>
    StorageUnavailable = 11,

    /// <summary>
    /// La validación de recuperación terminó pausando el lote por inconsistencia (Anexo F.5). No es un éxito:
    /// quien lo consuma no puede leerlo como "validó bien".
    /// </summary>
    PausedByInconsistency = 12,

    /// <summary>
    /// Se pidió volver a <c>PENDIENTE</c> un envío ya confirmado: eso lo dejaría otra vez "pendiente de
    /// enviar" y reenviable, contra §2.8 reglas 6 y 12.
    /// </summary>
    InvalidSendTransition = 13,
}

/// <summary>Resultado de una operación sin valor de retorno, con su motivo y un detalle legible.</summary>
public sealed class BatchOperationResult
{
    private BatchOperationResult(BatchOutcome outcome, string? detail)
    {
        Outcome = outcome;
        Detail = detail;
    }

    /// <summary>Motivo del resultado.</summary>
    public BatchOutcome Outcome { get; }

    /// <summary>Detalle en español para el log y la interfaz; nunca contiene secretos.</summary>
    public string? Detail { get; }

    /// <summary>Indica si la operación se completó.</summary>
    public bool Succeeded => Outcome == BatchOutcome.Succeeded;

    /// <summary>Resultado exitoso.</summary>
    public static BatchOperationResult Success { get; } = new(BatchOutcome.Succeeded, null);

    /// <summary>Resultado exitoso con un detalle informativo.</summary>
    public static BatchOperationResult SuccessWith(string detail) => new(BatchOutcome.Succeeded, detail);

    /// <summary>Resultado fallido con su motivo y, opcionalmente, su detalle.</summary>
    public static BatchOperationResult Failed(BatchOutcome outcome, string? detail = null)
    {
        if (outcome == BatchOutcome.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.");
        }

        return new BatchOperationResult(outcome, detail);
    }
}

/// <summary>Resultado de una operación con un valor de retorno.</summary>
/// <typeparam name="T">Tipo del valor devuelto por la operación.</typeparam>
public sealed class BatchOperationResult<T>
    where T : class
{
    private BatchOperationResult(BatchOutcome outcome, T? value, string? detail)
    {
        Outcome = outcome;
        Value = value;
        Detail = detail;
    }

    /// <summary>Motivo del resultado.</summary>
    public BatchOutcome Outcome { get; }

    /// <summary>Valor devuelto; nulo cuando la operación falló.</summary>
    public T? Value { get; }

    /// <summary>Detalle en español para el log y la interfaz; nunca contiene secretos.</summary>
    public string? Detail { get; }

    /// <summary>Indica si la operación se completó.</summary>
    public bool Succeeded => Outcome == BatchOutcome.Succeeded;

    /// <summary>Resultado con el valor devuelto.</summary>
    public static BatchOperationResult<T> Ok(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new BatchOperationResult<T>(BatchOutcome.Succeeded, value, null);
    }

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static BatchOperationResult<T> Failed(BatchOutcome outcome, string? detail = null)
    {
        if (outcome == BatchOutcome.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Un resultado fallido debe indicar un motivo.");
        }

        return new BatchOperationResult<T>(outcome, null, detail);
    }
}
