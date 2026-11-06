namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas del proxy (§1.1 y §2.9.2 r7-r9). Apagado, el campo de texto queda deshabilitado y vacío; el
/// proxy no lleva usuario ni contraseña porque el analista indicó que no se necesitan.
/// </summary>
public static class ProxySettingsValidation
{
    /// <summary>Nombre visible del interruptor de proxy.</summary>
    public const string ProxyParameter = "Proxy";

    /// <summary>Nombre visible del campo de texto del proxy.</summary>
    public const string ProxyTextParameter = "Texto del proxy";

    /// <summary>
    /// Con el interruptor apagado el texto no se usa: no es un error que esté vacío. Con el interruptor
    /// encendido el texto pasa a ser obligatorio.
    /// </summary>
    /// <param name="enabled">Estado del interruptor.</param>
    /// <param name="text">Texto del proxy tal como lo ve el analista.</param>
    public static ValidationResult ValidateText(bool enabled, string? text)
    {
        if (!enabled)
        {
            return ValidationResult.Valid;
        }

        return string.IsNullOrWhiteSpace(text)
            ? ValidationResult.Invalid(ProxyTextParameter, "es obligatorio cuando el proxy está activado.")
            : ValidationResult.Valid;
    }
}
