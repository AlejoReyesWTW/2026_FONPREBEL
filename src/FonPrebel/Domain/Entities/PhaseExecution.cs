namespace FonPrebel.Domain.Entities;

/// <summary>Estado de una fase dentro de una ejecución (§2.8, tabla <c>PhaseExecution</c> del §3.3).</summary>
public sealed class PhaseExecution
{
    /// <summary>Clave técnica.</summary>
    public int Id { get; set; }

    /// <summary>Ejecución a la que pertenece la fase.</summary>
    public int BatchExecutionId { get; set; }

    /// <summary>Número de fase, de 1 a 5.</summary>
    public int Phase { get; set; }

    /// <summary>Estado de la fase.</summary>
    public Enums.PhaseStatus Status { get; set; }

    /// <summary>Momento de inicio, en UTC.</summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>Momento de fin, en UTC.</summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Avance de la fase.</summary>
    public int ProcessedCount { get; set; }

    /// <summary>Universo de la fase; nulo hasta conocerlo.</summary>
    public int? TotalCount { get; set; }

    /// <summary>Código estable del último error, para métricas.</summary>
    public string? LastErrorCode { get; set; }

    /// <summary>Mensaje del último error para la interfaz y <c>errors.log</c>; nunca contiene secretos.</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>Ejecución de la fase.</summary>
    public BatchExecution? BatchExecution { get; set; }
}
