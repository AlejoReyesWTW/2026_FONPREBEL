namespace FonPrebel.Domain.ValueObjects;

/// <summary>
/// Papel de un archivo de entrada dentro de una ejecución, tal como lo nombra la tabla
/// <c>ExecutionInputFingerprint</c> del §3.7 del diseño de datos.
/// </summary>
public static class InputFileRole
{
    /// <summary>Consolidado anual de la ruta 1 (§2.12).</summary>
    public const string Consolidated = "Consolidated";

    /// <summary>Archivo del analista de la ruta 3, que la fase 2 completa (§2.12).</summary>
    public const string AnalystFile = "AnalystFile";

    /// <summary>Archivo de póliza de la ruta 2.</summary>
    public const string PolicySource = "PolicySource";

    /// <summary>Los papeles definidos por el §3.7. Un papel fuera de esta lista no se registra.</summary>
    public static IReadOnlyList<string> Known { get; } = new[] { Consolidated, AnalystFile, PolicySource };

    /// <summary>Indica si el papel está definido en el diseño de datos.</summary>
    public static bool IsKnown(string? role) =>
        role is not null && Known.Contains(role, StringComparer.Ordinal);
}
