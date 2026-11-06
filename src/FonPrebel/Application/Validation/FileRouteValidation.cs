using System.IO;

namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas de las cuatro rutas del servidor de archivos (§1.2 del diseño de configuración): todas son
/// obligatorias, se comprueba que existan y, en las que la aplicación escribe, que se puedan escribir.
/// </summary>
/// <remarks>
/// La comprobación de escritura es real y no una suposición sobre permisos: en la ruta 3 se abre el
/// archivo para lectura y escritura, y en la ruta 4 se crea y se borra un archivo de sonda dentro de la
/// carpeta. Nada se modifica de forma permanente.
/// </remarks>
public static class FileRouteValidation
{
    /// <summary>Nombre visible de la ruta 1.</summary>
    public const string Route1Parameter = "Ruta 1 — consolidado anual";

    /// <summary>Nombre visible de la ruta 2.</summary>
    public const string Route2Parameter = "Ruta 2 — archivos de póliza";

    /// <summary>Nombre visible de la ruta 3.</summary>
    public const string Route3Parameter = "Ruta 3 — archivo del analista";

    /// <summary>Nombre visible de la ruta 4.</summary>
    public const string Route4Parameter = "Ruta 4 — PDFs";

    /// <summary>Ruta 1: el consolidado anual debe ser un archivo existente.</summary>
    public static ValidationResult ValidateConsolidatedFile(string? path) =>
        ValidateExistingFile(Route1Parameter, path);

    /// <summary>Ruta 2: la carpeta de los 16 archivos de póliza debe existir.</summary>
    public static ValidationResult ValidatePolicyFolder(string? path) =>
        ValidateExistingFolder(Route2Parameter, path);

    /// <summary>Ruta 3: el archivo del analista debe existir y poder escribirse.</summary>
    public static ValidationResult ValidateAnalystFile(string? path)
    {
        ValidationResult existing = ValidateExistingFile(Route3Parameter, path);
        if (!existing.IsValid)
        {
            return existing;
        }

        return CanOpenForWriting(path!.Trim())
            ? ValidationResult.Valid
            : ValidationResult.Invalid(Route3Parameter, "no se puede escribir: revisa los permisos del archivo.");
    }

    /// <summary>Ruta 4: la carpeta de PDFs debe existir y poder escribirse.</summary>
    public static ValidationResult ValidatePdfFolder(string? path)
    {
        ValidationResult existing = ValidateExistingFolder(Route4Parameter, path);
        if (!existing.IsValid)
        {
            return existing;
        }

        return CanCreateFile(path!.Trim())
            ? ValidationResult.Valid
            : ValidationResult.Invalid(Route4Parameter, "no se puede escribir: revisa los permisos de la carpeta.");
    }

    private static ValidationResult ValidateExistingFile(string parameterName, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid(parameterName, "es obligatoria.");
        }

        return File.Exists(path.Trim())
            ? ValidationResult.Valid
            : ValidationResult.Invalid(parameterName, "el archivo no existe.");
    }

    private static ValidationResult ValidateExistingFolder(string parameterName, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid(parameterName, "es obligatoria.");
        }

        return Directory.Exists(path.Trim())
            ? ValidationResult.Valid
            : ValidationResult.Invalid(parameterName, "la carpeta no existe.");
    }

    private static bool CanOpenForWriting(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            return stream.CanWrite;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool CanCreateFile(string folder)
    {
        string probe = Path.Combine(folder, $".fonprebel-{Guid.NewGuid():N}.tmp");

        try
        {
            using (FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(probe))
                {
                    File.Delete(probe);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // La sonda no se pudo borrar: no invalida la validación, y queda dentro de la carpeta elegida.
            }
        }
    }
}
