using System.Globalization;
using System.IO;

namespace FonPrebel.Infrastructure.FileStorage;

/// <summary>
/// Rutas locales de la aplicación (S5): una raíz única por usuario, con la base, las bitácoras
/// diarias y la carpeta de salida. Ninguna ruta absoluta está escrita en el código: la raíz se
/// recibe por constructor y el constructor sin parámetros usa <c>%LOCALAPPDATA%</c>. Las carpetas se
/// crean cuando se consultan, de modo que la app sólo necesita permisos de usuario.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Nombre de la carpeta de la aplicación dentro de los datos locales del usuario.</summary>
    public const string ApplicationFolderName = "FonPrebel";

    /// <summary>Subcarpeta de la base SQLite local.</summary>
    public const string DatabaseFolderName = "Database";

    /// <summary>Subcarpeta de las bitácoras, una por día.</summary>
    public const string LogsFolderName = "Logs";

    /// <summary>Subcarpeta de los informes y PDFs generados.</summary>
    public const string SalidaFolderName = "Salida";

    /// <summary>Nombre exacto de la bitácora de ejecución.</summary>
    public const string ExecutionLogFileName = "execution.log";

    /// <summary>Nombre exacto de la bitácora de errores.</summary>
    public const string ErrorLogFileName = "errors.log";

    /// <summary>Formato de la carpeta diaria de bitácoras.</summary>
    public const string DayFolderFormat = "yyyy-MM-dd";

    /// <summary>Raíz por defecto: <c>%LOCALAPPDATA%\FonPrebel</c> del usuario que ejecuta la app.</summary>
    public AppPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName))
    {
    }

    /// <summary>Crea las rutas sobre la raíz indicada; permite probar con un directorio temporal.</summary>
    public AppPaths(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("La raíz de datos locales no puede estar vacía.", nameof(root));
        }

        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Raíz de los datos locales del usuario.</summary>
    public string Root { get; }

    /// <summary>Carpeta de la base SQLite local.</summary>
    public string DatabaseDirectory => Create(Path.Combine(Root, DatabaseFolderName));

    /// <summary>Carpeta de bitácoras; contiene una carpeta por día.</summary>
    public string LogsDirectory => Create(Path.Combine(Root, LogsFolderName));

    /// <summary>Carpeta de informes y PDFs generados.</summary>
    public string SalidaDirectory => Create(Path.Combine(Root, SalidaFolderName));

    /// <summary>Carpeta de bitácoras del día indicado, con formato <c>YYYY-MM-DD</c>.</summary>
    public string LogsDirectoryFor(DateOnly date) =>
        Create(Path.Combine(LogsDirectory, date.ToString(DayFolderFormat, CultureInfo.InvariantCulture)));

    /// <summary>Ruta de <c>execution.log</c> para el día indicado.</summary>
    public string ExecutionLogPath(DateOnly date) => Path.Combine(LogsDirectoryFor(date), ExecutionLogFileName);

    /// <summary>Ruta de <c>errors.log</c> para el día indicado.</summary>
    public string ErrorLogPath(DateOnly date) => Path.Combine(LogsDirectoryFor(date), ErrorLogFileName);

    private static string Create(string directory)
    {
        Directory.CreateDirectory(directory);

        return directory;
    }
}
