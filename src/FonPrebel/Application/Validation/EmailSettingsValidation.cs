using System.Net.Mail;

namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas de la configuración de correo (§1.1 y §2.9.2 del diseño de configuración). El puerto es
/// obligatorio siempre, con o sin proxy; la cuenta y la contraseña dependen del mecanismo de
/// autenticación, que sigue pendiente (§2.10), así que no se marcan obligatorias por cuenta propia.
/// </summary>
public static class EmailSettingsValidation
{
    /// <summary>Nombre visible del servidor de correo.</summary>
    public const string HostParameter = "Servidor de correo";

    /// <summary>Nombre visible del puerto.</summary>
    public const string PortParameter = "Puerto del correo";

    /// <summary>Nombre visible de la seguridad de la conexión.</summary>
    public const string SecurityParameter = "Seguridad de la conexión";

    /// <summary>Nombre visible del remitente.</summary>
    public const string SenderParameter = "Remitente";

    /// <summary>Nombre visible de la cuenta.</summary>
    public const string AccountParameter = "Cuenta de correo";

    /// <summary>Nombre visible del tiempo máximo de espera por envío.</summary>
    public const string SendTimeoutParameter = "Tiempo máximo de espera por envío";

    /// <summary>Puerto mínimo aceptable.</summary>
    public const int MinimumPort = 1;

    /// <summary>Puerto máximo aceptable.</summary>
    public const int MaximumPort = 65535;

    private const string PortRequirement = "un número entero entre 1 y 65535";

    private const string SendTimeoutRequirement = "un número entero positivo en segundos";

    /// <summary>El servidor es obligatorio y no puede quedar vacío.</summary>
    public static ValidationResult ValidateHost(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ValidationResult.Invalid(HostParameter, "es obligatorio.")
            : ValidationResult.Valid;

    /// <summary>El puerto es obligatorio siempre, entero y dentro del rango válido (§2.9.2 r8).</summary>
    public static ValidationResult ValidatePort(string? value) =>
        NumericText.ValidateInteger(PortParameter, value, MinimumPort, MaximumPort, PortRequirement);

    /// <summary>La seguridad de la conexión es obligatoria; sus valores siguen pendientes (§2.9.1).</summary>
    public static ValidationResult ValidateSecurity(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ValidationResult.Invalid(SecurityParameter, "es obligatoria.")
            : ValidationResult.Valid;

    /// <summary>El remitente es obligatorio y debe tener formato de dirección de correo.</summary>
    public static ValidationResult ValidateSender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Invalid(SenderParameter, "es obligatorio.");
        }

        return IsEmailAddress(value.Trim())
            ? ValidationResult.Valid
            : ValidationResult.Invalid(SenderParameter, "no es una dirección de correo válida.");
    }

    /// <summary>
    /// La cuenta sólo es obligatoria si el mecanismo confirmado autentica con usuario. Mientras el
    /// protocolo esté pendiente un valor vacío es aceptable (§2.10).
    /// </summary>
    /// <param name="value">Cuenta escrita por el analista.</param>
    /// <param name="requiredByProtocol">Verdadero sólo si TI confirmó un mecanismo con usuario.</param>
    public static ValidationResult ValidateAccount(string? value, bool requiredByProtocol)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Valid;
        }

        return requiredByProtocol
            ? ValidationResult.Invalid(AccountParameter, "es obligatoria con el mecanismo de autenticación confirmado.")
            : ValidationResult.Valid;
    }

    /// <summary>El tiempo máximo de espera por envío es obligatorio y se escribe en segundos.</summary>
    public static ValidationResult ValidateSendTimeout(string? value) =>
        NumericText.ValidateInteger(SendTimeoutParameter, value, 1, int.MaxValue, SendTimeoutRequirement);

    private static bool IsEmailAddress(string value)
    {
        try
        {
            MailAddress address = new(value);

            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
