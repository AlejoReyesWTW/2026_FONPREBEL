namespace FonPrebel.Application.Validation;

/// <summary>
/// Resultado de validar un parámetro de configuración (Anexo E). Todo resultado inválido trae el
/// nombre del parámetro en el mensaje, porque el spec prohíbe el "campo inválido" genérico.
/// </summary>
/// <param name="IsValid">Indica si el valor es aceptable.</param>
/// <param name="Message">Mensaje concreto y visible; vacío cuando el valor es válido.</param>
public sealed record ValidationResult(bool IsValid, string Message)
{
    /// <summary>Resultado válido, sin mensaje.</summary>
    public static ValidationResult Valid { get; } = new(true, string.Empty);

    /// <summary>Resultado inválido cuyo mensaje empieza con el nombre del parámetro.</summary>
    /// <param name="parameterName">Nombre visible del parámetro.</param>
    /// <param name="detail">Qué falta o está mal, sin datos sensibles.</param>
    public static ValidationResult Invalid(string parameterName, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return new ValidationResult(false, $"{parameterName}: {detail}");
    }
}
