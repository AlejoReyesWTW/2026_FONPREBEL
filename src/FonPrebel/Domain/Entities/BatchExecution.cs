namespace FonPrebel.Domain.Entities;

/// <summary>
/// Ejecución o reanudación de un lote (§2.8, tabla <c>BatchExecution</c> del §3.2 del diseño). Reanudar
/// crea una fila nueva del mismo lote, nunca un lote nuevo (§2.8 regla 5).
/// </summary>
public sealed class BatchExecution
{
    /// <summary>Clave técnica.</summary>
    public int Id { get; set; }

    /// <summary>Lote al que pertenece la ejecución.</summary>
    public int BatchId { get; set; }

    /// <summary>Número de ejecución dentro del lote: 1, 2, 3…</summary>
    public int Number { get; set; }

    /// <summary>Estado de la ejecución según el Anexo F.3.</summary>
    public Enums.BatchStatus Status { get; set; }

    /// <summary>Momento de inicio, en UTC.</summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>Momento de fin; nulo mientras la ejecución corre.</summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Última confirmación persistida como punto de control (Anexo F.1).</summary>
    public DateTime? LastCheckpointAtUtc { get; set; }

    /// <summary>Ejecución anterior, cuando esta fila es una reanudación.</summary>
    public int? ResumedFromExecutionId { get; set; }

    /// <summary>Lote de la ejecución.</summary>
    public Batch? Batch { get; set; }

    /// <summary>Ejecución de la que se reanudó, si aplica.</summary>
    public BatchExecution? ResumedFromExecution { get; set; }

    /// <summary>Estado persistido por fase.</summary>
    public ICollection<PhaseExecution> Phases { get; set; } = new List<PhaseExecution>();

    /// <summary>Huellas de los archivos de entrada usados por la ejecución (Anexo F.4).</summary>
    public ICollection<ExecutionInputFingerprint> InputFingerprints { get; set; } = new List<ExecutionInputFingerprint>();
}
