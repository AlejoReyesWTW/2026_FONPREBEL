namespace FonPrebel.Infrastructure.Logging;

/// <summary>
/// Nombres de las propiedades enriquecidas que exige S17 en cada línea de bitácora. Se escriben en
/// español porque son los nombres visibles del archivo de log, no identificadores internos.
/// </summary>
public static class LogPropertyNames
{
    /// <summary>Identificador legible del lote en curso.</summary>
    public const string Lote = "Lote";

    /// <summary>Número o identificador de la ejecución en curso.</summary>
    public const string Ejecucion = "Ejecucion";

    /// <summary>Fase del proceso (1 a 5).</summary>
    public const string Fase = "Fase";

    /// <summary>Resultado de la operación registrada.</summary>
    public const string Resultado = "Resultado";

    /// <summary>Orden en que las propiedades aparecen en la línea del archivo.</summary>
    public static readonly IReadOnlyList<string> Ordered = new[] { Lote, Ejecucion, Fase, Resultado };
}
