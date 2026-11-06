namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Punto de extensión para la comprobación real del correo (§2.14). Todavía no tiene implementación a
/// propósito: el mecanismo autorizado por el banco —SMTP, relay o autenticación integrada— sigue
/// pendiente (§2.10, §5.2), y un ping no demuestra ni la autenticación ni el envío.
/// </summary>
/// <remarks>
/// Cuando TI defina el mecanismo, se implementa esta interfaz, se registra en el contenedor y se le
/// pasa a <c>EmailConnectionProbe</c>: mientras no exista implementación, la sonda informa
/// <see cref="ConnectionProbeStatus.Unknown"/> con el motivo "protocolo de correo pendiente" en vez de
/// afirmar que el correo funciona. Una implementación nunca debe reportar verde sin haber completado el
/// mecanismo real de autenticación.
/// </remarks>
public interface IEmailConnectionCheck
{
    /// <summary>Comprueba el correo con la configuración guardada y devuelve el resultado tipado.</summary>
    Task<ConnectionProbeResult> CheckAsync(EmailSettings settings, CancellationToken cancellationToken = default);
}
