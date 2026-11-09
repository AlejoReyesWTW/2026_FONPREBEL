using System.IO;
using System.IO.Compression;

namespace FonPrebel.Tests.Delivery;

/// <summary>
/// S21 y la entrega del aplicativo: el publicado autocontenido tiene que llevar el runtime
/// adentro, no puede arrastrar la ruta de la máquina de desarrollo y su salida no se versiona.
/// Estas pruebas leen los scripts, así que atrapan el día que alguien los simplifique de más.
/// </summary>
public sealed class DeliveryScriptTests
{
    [Fact]
    public void The_publish_script_builds_a_self_contained_win_x64_package_and_fails_without_the_runtime()
    {
        string script = Read("tools/publicar.ps1");

        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("-r win-x64", script, StringComparison.Ordinal);

        foreach (string runtimeFile in new[] { "hostfxr.dll", "coreclr.dll", "e_sqlite3.dll" })
        {
            Assert.Contains($"'{runtimeFile}'", script, StringComparison.Ordinal);
        }

        Assert.Contains("$LASTEXITCODE -ne 0", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_publish_script_keeps_the_build_machine_path_out_of_the_package()
    {
        string script = Read("tools/publicar.ps1");

        // Sin esto, el bloque de depuración del PE lleva la ruta absoluta del .pdb de esta máquina.
        Assert.Contains("-p:DebugType=None", script, StringComparison.Ordinal);
        Assert.Contains("obj/Release", script, StringComparison.Ordinal);
        Assert.Contains("*.pdb", script, StringComparison.Ordinal);
    }

    [Fact]
    public void No_delivery_script_calls_a_binary_that_git_for_windows_can_shadow()
    {
        // El "timeout" de Git for Windows gana en el PATH y rompía run.bat; ver el commit 4eb7876.
        foreach (string script in new[] { "run.bat", "publicar.bat" })
        {
            foreach (string line in Read(script).Split('\n'))
            {
                string trimmed = line.TrimStart();

                Assert.False(
                    trimmed.StartsWith("timeout ", StringComparison.OrdinalIgnoreCase),
                    $"{script} llama a 'timeout' sin ruta absoluta: {trimmed.Trim()}");
            }
        }
    }

    [Fact]
    public void The_publish_output_is_not_versioned()
    {
        string[] ignored = Read(".gitignore")
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();

        Assert.Contains("publish/", ignored);
    }

    [Fact]
    public void The_package_that_was_built_matches_its_folder_and_carries_the_runtime()
    {
        string publish = Path.Combine(Root(), "publish");
        string folder = Path.Combine(publish, "FonPrebel");
        string archive = Path.Combine(publish, "FonPrebel-win-x64.zip");

        if (!Directory.Exists(folder) || !File.Exists(archive))
        {
            // publish/ no se versiona: en un clon limpio no existe y no hay nada que comparar.
            return;
        }

        using ZipArchive zip = ZipFile.OpenRead(archive);

        HashSet<string> inZip = zip.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> onDisk = Directory
            .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(onDisk.OrderBy(name => name), inZip.OrderBy(name => name));

        foreach (string file in new[] { "FonPrebel.exe", "hostfxr.dll", "coreclr.dll", "e_sqlite3.dll" })
        {
            Assert.Contains(file, inZip);
        }

        Assert.DoesNotContain(inZip, name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inZip, name => name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inZip, name => name.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inZip, name => name.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FonPrebel.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath));
}
