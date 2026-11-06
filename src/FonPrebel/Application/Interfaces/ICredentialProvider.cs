namespace FonPrebel.Application.Interfaces;

/// <summary>
/// S7 (§2.10): protege el secreto de correo antes de guardarlo y lo desprotege bajo demanda, en
/// memoria. La contraseña nunca se escribe en claro en SQLite, en las bitácoras ni en la pantalla.
/// </summary>
/// <remarks>
/// El caso "no se puede descifrar en este perfil" es esperado (§2.10: cambió el usuario o el perfil de
/// Windows) y por eso la desprotección no lanza excepciones: devuelve
/// <see cref="CredentialFailure.NotRecoverable"/>.
/// </remarks>
public interface ICredentialProvider
{
    /// <summary>Cifra el secreto en claro y devuelve el contenido protegido.</summary>
    /// <param name="secret">Secreto en claro; se rechaza vacío.</param>
    CredentialResult<byte[]> Protect(string secret);

    /// <summary>Descifra el contenido protegido y devuelve el secreto en claro, sólo en memoria.</summary>
    /// <param name="protectedValue">Contenido cifrado tal como está guardado.</param>
    CredentialResult<string> Unprotect(byte[] protectedValue);
}
