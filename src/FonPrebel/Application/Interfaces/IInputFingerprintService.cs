using FonPrebel.Domain.Entities;

namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Huellas de los archivos de entrada de una ejecución (Anexo F.4, §3.7 del diseño de datos). Se calculan
/// sobre el contenido del archivo, no sobre su nombre ni su ubicación, y se recalculan y comparan al
/// reanudar (Anexo F.9).
/// </summary>
public interface IInputFingerprintService
{
    /// <summary>
    /// Calcula y guarda la huella de cada archivo de entrada de la ejecución. Un archivo ausente o
    /// ilegible no se registra: se informa <see cref="BatchOutcome.InputNotAvailable"/> y no se toca nada.
    /// Volver a capturar la misma huella no duplica filas: la reemplaza.
    /// </summary>
    Task<BatchOperationResult> CaptureAsync(
        int executionId,
        IReadOnlyList<InputFileDescriptor> files,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcula cada huella registrada y la compara con la guardada. Una lista vacía significa que la
    /// ejecución no tiene huellas registradas, y eso no es una coincidencia.
    /// </summary>
    Task<BatchOperationResult<IReadOnlyList<InputFingerprintComparison>>> VerifyAsync(
        int executionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Archivo de entrada que se va a usar en una ejecución, con su papel en el proceso.</summary>
public sealed record InputFileDescriptor(string Role, string FilePath);

/// <summary>
/// Resultado de comparar un archivo de entrada con la huella registrada. Un archivo no disponible queda en
/// <see cref="Unavailable"/>: <b>no</b> es una coincidencia (Anexo F.4 y F.5).
/// </summary>
public enum InputFingerprintStatus
{
    /// <summary>El contenido, el tamaño y la fecha coinciden con la huella registrada.</summary>
    Matches = 0,

    /// <summary>El archivo cambió respecto de la huella registrada: se bloquea la reanudación (Anexo F.2.2).</summary>
    Modified = 1,

    /// <summary>El archivo no está o no se puede leer: la huella no es verificable.</summary>
    Unavailable = 2,
}

/// <summary>Comparación de un archivo de entrada con su huella, con el motivo en español para el log y la alerta.</summary>
public sealed record InputFingerprintComparison(
    string Role,
    string FilePath,
    InputFingerprintStatus Status,
    string Reason);

/// <summary>Huella calculada de un archivo de entrada.</summary>
public sealed record FileFingerprint(string FilePath, long SizeBytes, DateTime ModifiedAtUtc, string Sha256);

/// <summary>Resultado de intentar leer un archivo para calcular su huella.</summary>
public enum FileReadStatus
{
    /// <summary>El archivo se leyó y la huella se calculó.</summary>
    Ready = 0,

    /// <summary>El archivo no existe en la ruta indicada.</summary>
    Missing = 1,

    /// <summary>El archivo existe pero no se pudo leer (permisos, uso exclusivo, error de disco).</summary>
    Unreadable = 2,
}

/// <summary>Resultado de calcular la huella de un archivo, con el motivo cuando no se pudo.</summary>
public sealed record FileFingerprintResult(FileReadStatus Status, FileFingerprint? Fingerprint, string? Reason)
{
    /// <summary>Indica si la huella se calculó.</summary>
    public bool Succeeded => Status == FileReadStatus.Ready && Fingerprint is not null;
}

/// <summary>
/// Calcula la huella <c>SHA-256</c> de un archivo leyéndolo en streaming, sin cargarlo entero en memoria.
/// </summary>
public interface IFileFingerprintReader
{
    /// <summary>Calcula la huella del archivo o informa por qué no es verificable.</summary>
    Task<FileFingerprintResult> ComputeAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>Persistencia de las huellas de entrada de una ejecución (§3.7).</summary>
public interface IInputFingerprintStore
{
    /// <summary>
    /// Registra las huellas de la ejecución. Una huella del mismo papel reemplaza a la anterior, de modo
    /// que repetir la captura no duplica filas (Anexo F.9).
    /// </summary>
    Task<BatchOperationResult> SaveAsync(
        int executionId,
        IReadOnlyList<ExecutionInputFingerprint> fingerprints,
        CancellationToken cancellationToken = default);

    /// <summary>Huellas registradas de la ejecución, sin quedar enganchadas al contexto.</summary>
    Task<BatchOperationResult<IReadOnlyList<ExecutionInputFingerprint>>> ListAsync(
        int executionId,
        CancellationToken cancellationToken = default);
}
