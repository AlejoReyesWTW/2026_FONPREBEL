using System.Security.Cryptography;
using System.Text;
using FonPrebel.Application.Interfaces;

namespace FonPrebel.Infrastructure.Security;

/// <summary>
/// S7 (§2.10): protege el secreto de correo con DPAPI en el ámbito <c>CurrentUser</c> y una entropía
/// adicional fija de la aplicación. Desprotege bajo demanda, sólo en memoria.
/// </summary>
/// <remarks>
/// El ámbito <c>CurrentUser</c> está propuesto en el diseño y todavía debe validarse con TI (§2.10).
/// Ningún mensaje, excepción o bitácora de esta clase incluye el secreto, fragmentos del secreto ni su
/// longitud: un fallo de descifrado se devuelve como
/// <see cref="CredentialFailure.NotRecoverable"/>.
/// </remarks>
public sealed class DpapiCredentialProvider : ICredentialProvider
{
    /// <summary>
    /// Entropía adicional fija de la aplicación: privada y no configurable a propósito (§2.9.1: los
    /// parámetros los entrega TI, no se inventan). Cambiar su valor invalida los secretos ya guardados,
    /// por eso lleva versión en el texto.
    /// </summary>
    private static readonly byte[] ApplicationEntropy =
        Encoding.UTF8.GetBytes("FonPrebel.EmailCredential.v1");

    public CredentialResult<byte[]> Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return CredentialResult<byte[]>.Failed(CredentialFailure.InvalidInput);
        }

        byte[] plain = Encoding.UTF8.GetBytes(secret);

        try
        {
            return CredentialResult<byte[]>.Success(
                ProtectedData.Protect(plain, ApplicationEntropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return CredentialResult<byte[]>.Failed(CredentialFailure.ProtectionFailed);
        }
        catch (PlatformNotSupportedException)
        {
            return CredentialResult<byte[]>.Failed(CredentialFailure.ProtectionFailed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public CredentialResult<string> Unprotect(byte[] protectedValue)
    {
        if (protectedValue is null || protectedValue.Length == 0)
        {
            return CredentialResult<string>.Failed(CredentialFailure.InvalidInput);
        }

        byte[] plain;

        try
        {
            plain = ProtectedData.Unprotect(protectedValue, ApplicationEntropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Contenido corrupto, de otra entropía o de otro perfil de Windows: hay que reingresarlo.
            return CredentialResult<string>.Failed(CredentialFailure.NotRecoverable);
        }
        catch (PlatformNotSupportedException)
        {
            return CredentialResult<string>.Failed(CredentialFailure.NotRecoverable);
        }

        try
        {
            return CredentialResult<string>.Success(Encoding.UTF8.GetString(plain));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
