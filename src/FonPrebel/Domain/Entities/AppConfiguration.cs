namespace FonPrebel.Domain.Entities;

/// <summary>
/// Parámetro general de la aplicación (§2.9, tabla <c>AppConfiguration</c> del §3.6). El modelo es
/// clave/valor a propósito: la lista de parámetros está pendiente de confirmación (§2.9.1).
/// </summary>
/// <remarks>
/// <c>Value</c> nulo significa "pendiente de configurar" y la interfaz lo muestra vacío: ningún
/// parámetro tiene un valor por defecto inventado (S6, §6 regla 2 del spec).
/// </remarks>
public sealed class AppConfiguration
{
    /// <summary>Clave del parámetro; clave primaria de la tabla.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Valor del parámetro; nulo mientras esté pendiente.</summary>
    public string? Value { get; set; }

    /// <summary>Momento de la última actualización, en UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Usuario que actualizó el parámetro, si se conoce.</summary>
    public string? UpdatedBy { get; set; }
}
