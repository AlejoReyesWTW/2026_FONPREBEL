using System.IO;
using System.Text;

namespace FonPrebel.Infrastructure.Logging;

/// <summary>
/// Lectura de bitácoras para la pantalla Logs (S17). Abre el archivo compartido, de modo que se pueda
/// leer mientras la aplicación escribe, y tolera que no exista: devuelve una lista vacía.
/// </summary>
public static class LogFileReader
{
    /// <summary>Devuelve como máximo <paramref name="maximumLines"/> líneas, las últimas del archivo.</summary>
    public static IReadOnlyList<string> ReadLastLines(string path, int maximumLines)
    {
        if (string.IsNullOrWhiteSpace(path) || maximumLines <= 0 || !File.Exists(path))
        {
            return Array.Empty<string>();
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        Queue<string> lines = new(Math.Min(maximumLines, 256));

        while (reader.ReadLine() is { } line)
        {
            if (lines.Count == maximumLines)
            {
                lines.Dequeue();
            }

            lines.Enqueue(line);
        }

        return lines.ToArray();
    }
}
