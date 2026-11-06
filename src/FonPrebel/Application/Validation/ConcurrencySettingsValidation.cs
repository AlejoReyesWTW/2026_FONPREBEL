namespace FonPrebel.Application.Validation;

/// <summary>
/// Reglas de intervalos y concurrencia (§1.3 del diseño de configuración). Todos los parámetros son
/// obligatorios y numéricos, pero su valor sigue pendiente: la pantalla los muestra vacíos y no los
/// inventa. El único valor confirmado por el spec es el intervalo de verificación de 2 minutos.
/// </summary>
public static class ConcurrencySettingsValidation
{
    /// <summary>Nombre visible del intervalo de verificación automática.</summary>
    public const string AutoCheckParameter = "Intervalo de verificación automática";

    /// <summary>Nombre visible del umbral de conexión lenta.</summary>
    public const string SlowThresholdParameter = "Umbral de conexión lenta";

    /// <summary>Nombre visible de los trabajadores de PDF.</summary>
    public const string PdfWorkersParameter = "Trabajadores para PDFs";

    /// <summary>Nombre visible de los trabajadores de correo.</summary>
    public const string EmailWorkersParameter = "Trabajadores para correos";

    private const string AutoCheckRequirement = "un número entero positivo en minutos";

    private const string SlowThresholdRequirement = "un número entero positivo en milisegundos";

    private const string WorkersRequirement = "un número entero mayor o igual a 1";

    /// <summary>El intervalo de verificación automática es un entero positivo en minutos.</summary>
    public static ValidationResult ValidateAutoCheckMinutes(string? value) =>
        NumericText.ValidateInteger(AutoCheckParameter, value, 1, int.MaxValue, AutoCheckRequirement);

    /// <summary>El umbral de conexión lenta es un entero positivo en milisegundos; su valor está pendiente.</summary>
    public static ValidationResult ValidateSlowThresholdMs(string? value) =>
        NumericText.ValidateInteger(SlowThresholdParameter, value, 1, int.MaxValue, SlowThresholdRequirement);

    /// <summary>La cantidad de trabajadores de PDF es un entero mayor o igual a 1.</summary>
    public static ValidationResult ValidatePdfWorkers(string? value) =>
        NumericText.ValidateInteger(PdfWorkersParameter, value, 1, int.MaxValue, WorkersRequirement);

    /// <summary>La cantidad de trabajadores de correo es un entero mayor o igual a 1.</summary>
    public static ValidationResult ValidateEmailWorkers(string? value) =>
        NumericText.ValidateInteger(EmailWorkersParameter, value, 1, int.MaxValue, WorkersRequirement);
}
