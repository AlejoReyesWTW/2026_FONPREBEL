using System.Globalization;

namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas numéricas compartidas por los validadores de configuración: un solo lugar decide cómo se
/// interpreta un entero escrito por el analista y qué mensaje se muestra cuando no sirve.
/// </summary>
public static class NumericText
{
    /// <summary>Interpreta el texto como entero con cultura invariante, admitiendo espacios alrededor.</summary>
    public static bool TryParseInt(string? value, out int result)
    {
        result = 0;

        return !string.IsNullOrWhiteSpace(value) &&
               int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Valida un entero obligatorio dentro del rango indicado. <paramref name="requirement"/> describe el
    /// valor esperado, por ejemplo "un número entero positivo (minutos)".
    /// </summary>
    public static ValidationResult ValidateInteger(
        string parameterName,
        string? value,
        int minimum,
        int maximum,
        string requirement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Invalid(parameterName, $"es obligatorio: debe ser {requirement}.");
        }

        if (!TryParseInt(value, out int number) || number < minimum || number > maximum)
        {
            return ValidationResult.Invalid(parameterName, $"debe ser {requirement}.");
        }

        return ValidationResult.Valid;
    }
}
