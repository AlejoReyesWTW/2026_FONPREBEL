using System.IO;

namespace FonPrebel.Tests;

/// <summary>
/// Carpeta temporal propia de cada prueba, fuera del repositorio, que se borra al terminar. Ninguna
/// prueba toca <c>%LOCALAPPDATA%</c> real.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "FonPrebel.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Un archivo todavía abierto no invalida la prueba: la carpeta vive en el directorio temporal.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
