using System.IO;
using System.Security.Cryptography;
using FonPrebel.Application.Interfaces;

namespace FonPrebel.Application.Services;

/// <summary>
/// Anexo F.4: huella <c>SHA-256</c> del contenido de un archivo de entrada, calculada en streaming desde un
/// <see cref="FileStream"/> acotado, sin cargar el archivo entero en memoria.
/// </summary>
/// <remarks>
/// Un archivo ausente o ilegible <b>no</b> produce una huella: se informa su motivo para que la validación de
/// recuperación lo trate como no verificable y no como coincidencia (Anexo F.4 y F.5).
/// </remarks>
public sealed class FileFingerprintReader : IFileFingerprintReader
{
    private const int BufferSize = 64 * 1024;

    /// <inheritdoc />
    public async Task<FileFingerprintResult> ComputeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new FileFingerprintResult(
                FileReadStatus.Unreadable,
                null,
                "No se indicó la ruta del archivo de entrada: la huella no se puede calcular.");
        }

        if (!File.Exists(filePath))
        {
            return new FileFingerprintResult(
                FileReadStatus.Missing,
                null,
                $"El archivo de entrada no está disponible en {filePath}: la huella no se puede verificar.");
        }

        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);

            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            FileInfo info = new(filePath);

            return new FileFingerprintResult(
                FileReadStatus.Ready,
                new FileFingerprint(
                    info.FullName,
                    stream.Length,
                    info.LastWriteTimeUtc,
                    Convert.ToHexString(hash)),
                null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FileFingerprintResult(
                FileReadStatus.Unreadable,
                null,
                $"No se pudo leer el archivo de entrada {filePath}: {exception.Message}");
        }
    }
}
