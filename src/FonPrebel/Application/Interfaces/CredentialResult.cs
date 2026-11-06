namespace FonPrebel.Application.Interfaces;

/// <summary>
/// S7 (§2.10): resultado de proteger o desproteger un secreto. Un fallo de descifrado es un escenario
/// esperado —cambio de usuario o de perfil de Windows (§2.10)— y por eso se informa como valor y no
/// como excepción, para que la pantalla ofrezca reingresar la credencial sin perder el historial.
/// </summary>
/// <remarks>
/// El resultado nunca transporta el secreto en un mensaje: <see cref="Value"/> es el único portador y
/// sólo cuando <see cref="Succeeded"/> es verdadero.
/// </remarks>
public enum CredentialFailure
{
    /// <summary>La operación devolvió un valor.</summary>
    None = 0,

    /// <summary>El secreto o el contenido cifrado llegaron vacíos.</summary>
    InvalidInput = 1,

    /// <summary>DPAPI no pudo cifrar el secreto.</summary>
    ProtectionFailed = 2,

    /// <summary>DPAPI no puede descifrar el contenido en este perfil de Windows; hay que reingresarlo.</summary>
    NotRecoverable = 3,
}

/// <summary>Resultado tipado de una operación de protección o desprotección de un secreto (S7).</summary>
/// <typeparam name="T">Texto en claro o contenido cifrado, según la operación.</typeparam>
public sealed class CredentialResult<T>
{
    private CredentialResult(CredentialFailure failure, T? value)
    {
        Failure = failure;
        Value = value;
    }

    /// <summary>Motivo del fallo; <see cref="CredentialFailure.None"/> cuando la operación tuvo éxito.</summary>
    public CredentialFailure Failure { get; }

    /// <summary>Valor devuelto por la operación; nulo cuando falló.</summary>
    public T? Value { get; }

    /// <summary>Indica si la operación devolvió un valor utilizable.</summary>
    public bool Succeeded => Failure == CredentialFailure.None;

    /// <summary>Resultado con el valor de la operación.</summary>
    public static CredentialResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new CredentialResult<T>(CredentialFailure.None, value);
    }

    /// <summary>Resultado sin valor, con el motivo del fallo.</summary>
    public static CredentialResult<T> Failed(CredentialFailure failure)
    {
        if (failure == CredentialFailure.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failure), "El resultado fallido debe indicar un motivo.");
        }

        return new CredentialResult<T>(failure, default);
    }
}
