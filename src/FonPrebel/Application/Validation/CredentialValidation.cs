namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas del modo edición de la credencial de correo (§3.2 del diseño de configuración): los tres
/// campos son obligatorios y la confirmación debe coincidir con la contraseña nueva. La contraseña
/// anterior se compara con la guardada en <c>IEmailCredentialService</c>, no acá.
/// </summary>
public static class CredentialValidation
{
    /// <summary>Nombre visible de la contraseña anterior.</summary>
    public const string PreviousParameter = "Contraseña anterior";

    /// <summary>Nombre visible de la contraseña nueva.</summary>
    public const string NewParameter = "Contraseña nueva";

    /// <summary>Nombre visible de la confirmación.</summary>
    public const string ConfirmationParameter = "Confirmación de la contraseña nueva";

    /// <summary>La contraseña anterior es obligatoria para poder verificar el reemplazo.</summary>
    public static ValidationResult ValidatePrevious(string? previous) =>
        string.IsNullOrEmpty(previous)
            ? ValidationResult.Invalid(PreviousParameter, "es obligatoria.")
            : ValidationResult.Valid;

    /// <summary>La contraseña nueva es obligatoria.</summary>
    public static ValidationResult ValidateNew(string? newSecret) =>
        string.IsNullOrEmpty(newSecret)
            ? ValidationResult.Invalid(NewParameter, "es obligatoria.")
            : ValidationResult.Valid;

    /// <summary>La confirmación es obligatoria y debe coincidir con la contraseña nueva.</summary>
    public static ValidationResult ValidateConfirmation(string? newSecret, string? confirmation)
    {
        if (string.IsNullOrEmpty(confirmation))
        {
            return ValidationResult.Invalid(ConfirmationParameter, "es obligatoria.");
        }

        return string.Equals(confirmation, newSecret, StringComparison.Ordinal)
            ? ValidationResult.Valid
            : ValidationResult.Invalid(ConfirmationParameter, "no coincide con la contraseña nueva.");
    }

    /// <summary>Valida los tres campos del modo edición y devuelve el primer problema encontrado.</summary>
    public static ValidationResult ValidateAll(string? previous, string? newSecret, string? confirmation)
    {
        ValidationResult result = ValidatePrevious(previous);
        if (!result.IsValid)
        {
            return result;
        }

        result = ValidateNew(newSecret);
        if (!result.IsValid)
        {
            return result;
        }

        return ValidateConfirmation(newSecret, confirmation);
    }
}
