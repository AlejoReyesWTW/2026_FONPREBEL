using System.IO;
using System.Text;
using FonPrebel.Infrastructure.Logging;

namespace FonPrebel.Tests;

/// <summary>
/// Fija la lectura de bitácoras de S17: la UI tolera que el archivo no exista, lee las últimas
/// líneas y puede leer mientras la aplicación sigue escribiendo.
/// </summary>
public sealed class LogFileReaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_missing_file_returns_no_lines()
    {
        IReadOnlyList<string> lines = LogFileReader.ReadLastLines(Path.Combine(_temp.Path, "no-existe.log"), 10);

        Assert.Empty(lines);
    }

    [Fact]
    public void An_empty_file_returns_no_lines()
    {
        string path = WriteFile(string.Empty);

        Assert.Empty(LogFileReader.ReadLastLines(path, 10));
    }

    [Fact]
    public void Reading_one_line_returns_it()
    {
        string path = WriteFile("primera" + Environment.NewLine);

        Assert.Equal(new[] { "primera" }, LogFileReader.ReadLastLines(path, 10).ToArray());
    }

    [Fact]
    public void Reading_returns_at_most_the_requested_number_of_lines_starting_from_the_end()
    {
        string path = WriteFile(string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(number => $"línea {number}")));

        IReadOnlyList<string> lines = LogFileReader.ReadLastLines(path, 4);

        Assert.Equal(new[] { "línea 7", "línea 8", "línea 9", "línea 10" }, lines.ToArray());
    }

    [Fact]
    public void Reading_a_file_that_is_being_written_does_not_fail()
    {
        string path = Path.Combine(_temp.Path, "execution.log");

        using FileStream writer = new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using StreamWriter streamWriter = new(writer, new UTF8Encoding(false));
        streamWriter.WriteLine("entrada mientras se escribe");
        streamWriter.Flush();

        IReadOnlyList<string> lines = LogFileReader.ReadLastLines(path, 10);

        Assert.Equal(new[] { "entrada mientras se escribe" }, lines.ToArray());
    }

    [Fact]
    public void A_non_positive_limit_returns_no_lines()
    {
        string path = WriteFile("una" + Environment.NewLine);

        Assert.Empty(LogFileReader.ReadLastLines(path, 0));
    }

    private string WriteFile(string content)
    {
        string path = Path.Combine(_temp.Path, $"log-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, content);

        return path;
    }
}
