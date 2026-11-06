namespace FonPrebel.Domain.Entities;

/// <summary>
/// Huella de un archivo de entrada de una ejecución (Anexo F.4, tabla
/// <c>ExecutionInputFingerprint</c> del §3.7). Al reanudar se recalcula y se compara con la guardada.
/// </summary>
public sealed class ExecutionInputFingerprint
{
    /// <summary>Clave técnica.</summary>
    public int Id { get; set; }

    /// <summary>Ejecución que usó el archivo.</summary>
    public int BatchExecutionId { get; set; }

    /// <summary>Papel del archivo: <c>Consolidated</c>, <c>AnalystFile</c> o <c>PolicySource</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Ruta completa usada en la ejecución.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Tamaño del archivo en bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Momento de la última modificación del archivo, en UTC.</summary>
    public DateTime FileModifiedAtUtc { get; set; }

    /// <summary>Hash SHA-256 del contenido.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Momento en que se capturó la huella, en UTC.</summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Ejecución de la huella.</summary>
    public BatchExecution? BatchExecution { get; set; }
}
