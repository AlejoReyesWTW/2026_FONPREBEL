using System.Security.Cryptography;
using System.Text;
using FonPrebel.Application.Interfaces;
using FonPrebel.Infrastructure.Security;

namespace FonPrebel.Tests.Security;

/// <summary>
/// S7 (§2.10): el secreto se protege y se recupera con DPAPI <c>CurrentUser</c>, el texto en claro no
/// queda dentro del contenido cifrado y un fallo de descifrado se informa como resultado tipado,
/// nunca como excepción que llegue a la pantalla.
/// </summary>
public sealed class DpapiCredentialProviderTests
{
    /// <summary>Valor evidentemente ficticio: ninguna prueba usa una contraseña real.</summary>
    private const string TestSecret = "contrasena-de-prueba";

    [Fact]
    public void Protecting_and_unprotecting_returns_the_same_text()
    {
        DpapiCredentialProvider provider = new();

        CredentialResult<byte[]> protectedValue = provider.Protect(TestSecret);

        Assert.True(protectedValue.Succeeded);
        Assert.NotNull(protectedValue.Value);

        CredentialResult<string> restored = provider.Unprotect(protectedValue.Value!);

        Assert.True(restored.Succeeded);
        Assert.Equal(TestSecret, restored.Value);
    }

    [Fact]
    public void The_protected_content_does_not_contain_the_original_text()
    {
        DpapiCredentialProvider provider = new();

        byte[] protectedValue = provider.Protect(TestSecret).Value!;

        Assert.False(ContainsSequence(protectedValue, Encoding.UTF8.GetBytes(TestSecret)));
    }

    [Fact]
    public void Corrupted_or_foreign_bytes_are_reported_as_not_recoverable()
    {
        DpapiCredentialProvider provider = new();

        byte[] valid = provider.Protect(TestSecret).Value!;

        byte[] corrupted = (byte[])valid.Clone();
        corrupted[0] ^= 0xFF;
        corrupted[^1] ^= 0xFF;

        byte[] random = new byte[valid.Length];
        RandomNumberGenerator.Fill(random);

        // Cifrado por DPAPI con otra entropía: es un blob de este mismo usuario pero de otro origen.
        byte[] foreign = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(TestSecret),
            Encoding.UTF8.GetBytes("entropia-de-otro-origen"),
            DataProtectionScope.CurrentUser);

        foreach (byte[] candidate in new[] { corrupted, random, foreign })
        {
            CredentialResult<string> result = provider.Unprotect(candidate);

            Assert.Equal(CredentialFailure.NotRecoverable, result.Failure);
            Assert.False(result.Succeeded);
            Assert.Null(result.Value);
        }
    }

    [Fact]
    public void An_empty_secret_is_reported_as_invalid_input()
    {
        DpapiCredentialProvider provider = new();

        Assert.Equal(CredentialFailure.InvalidInput, provider.Protect(string.Empty).Failure);
        Assert.Equal(CredentialFailure.InvalidInput, provider.Unprotect(Array.Empty<byte>()).Failure);
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int start = 0; start <= haystack.Length - needle.Length; start++)
        {
            int offset = 0;
            while (offset < needle.Length && haystack[start + offset] == needle[offset])
            {
                offset++;
            }

            if (offset == needle.Length)
            {
                return true;
            }
        }

        return false;
    }
}
