namespace FonPrebel.Domain.Entities;

/// <summary>Jubilado dentro de un lote (§2.8, tabla <c>BatchRecipient</c> del §3.4 del diseño).</summary>
public sealed class BatchRecipient
{
    /// <summary>Clave técnica.</summary>
    public int Id { get; set; }

    /// <summary>Lote al que pertenece el jubilado.</summary>
    public int BatchId { get; set; }

    /// <summary>Cédula del asociado, normalizada sin puntos para comparar.</summary>
    public string Cedula { get; set; } = string.Empty;

    /// <summary>Nombre completo del asociado.</summary>
    public string? FullName { get; set; }

    /// <summary>Correo electrónico; nulo o vacío significa "no notificable".</summary>
    public string? Email { get; set; }

    /// <summary>Estado del PDF del jubilado.</summary>
    public Enums.PdfStatus PdfStatus { get; set; }

    /// <summary>Ruta del PDF generado; el nombre del archivo está pendiente.</summary>
    public string? PdfPath { get; set; }

    /// <summary>Estado del envío del correo.</summary>
    public Enums.SendStatus SendStatus { get; set; }

    /// <summary>Intentos de envío realizados con la política pendiente (§5.2).</summary>
    public int SendAttemptCount { get; set; }

    /// <summary>Momento del último intento, en UTC.</summary>
    public DateTime? LastAttemptAtUtc { get; set; }

    /// <summary>Momento de la confirmación de envío; sólo se escribe con confirmación (§2.8 regla 12).</summary>
    public DateTime? ConfirmedAtUtc { get; set; }

    /// <summary>Código estable del último error.</summary>
    public string? LastErrorCode { get; set; }

    /// <summary>Mensaje del último error; nunca contiene secretos.</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>Momento de la última actualización del registro, en UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Lote del jubilado.</summary>
    public Batch? Batch { get; set; }
}
