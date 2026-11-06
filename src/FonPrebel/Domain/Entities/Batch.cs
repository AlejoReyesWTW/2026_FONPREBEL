namespace FonPrebel.Domain.Entities;

/// <summary>Lote de trabajo mensual (§2.8, tabla <c>Batch</c> del §3.1 del diseño de datos).</summary>
public sealed class Batch
{
    /// <summary>Clave técnica.</summary>
    public int Id { get; set; }

    /// <summary>Identificador único y legible del lote (§2.8 regla 1); el formato está pendiente (§5.3).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Año del periodo del lote.</summary>
    public int PeriodYear { get; set; }

    /// <summary>Mes del periodo del lote, de 1 a 12.</summary>
    public int PeriodMonth { get; set; }

    /// <summary>Estado del lote según el Anexo F.3.</summary>
    public Enums.BatchStatus Status { get; set; }

    /// <summary>Momento de creación del lote, en UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Usuario de Windows que abrió el lote.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Observación libre del analista; no es una columna del Excel.</summary>
    public string? Notes { get; set; }

    /// <summary>Ejecuciones y reanudaciones del lote.</summary>
    public ICollection<BatchExecution> Executions { get; set; } = new List<BatchExecution>();

    /// <summary>Jubilados incluidos en el lote.</summary>
    public ICollection<BatchRecipient> Recipients { get; set; } = new List<BatchRecipient>();
}
