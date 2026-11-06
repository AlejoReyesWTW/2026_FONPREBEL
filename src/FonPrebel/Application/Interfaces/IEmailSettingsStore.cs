namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Configuración de correo de la fila única de <c>EmailConfiguration</c> (§3.5 del diseño de datos).
/// Todos los valores están pendientes de confirmar con TI (§2.9.1), así que pueden ser nulos.
/// </summary>
/// <param name="Host">Servidor de correo.</param>
/// <param name="Port">Puerto; el único de estos campos que es obligatorio siempre (§2.9.2 r8).</param>
/// <param name="Security">Seguridad de la conexión; sus valores siguen pendientes (§2.9.1).</param>
/// <param name="Account">Cuenta; nula si el mecanismo de autenticación no la exige (§2.10).</param>
/// <param name="SenderAddress">Remitente visible.</param>
public sealed record EmailSettings(
    string? Host,
    int? Port,
    string? Security,
    string? Account,
    string? SenderAddress)
{
    /// <summary>Configuración sin ningún valor confirmado: es el estado del primer arranque.</summary>
    public static EmailSettings Empty { get; } = new(null, null, null, null, null);
}

/// <summary>
/// S6: lectura y escritura de la configuración de correo. La contraseña no viaja por acá: se gestiona
/// cifrada con DPAPI en <see cref="IEmailCredentialService"/> (S7).
/// </summary>
public interface IEmailSettingsStore
{
    /// <summary>Lee la fila única de correo; el valor es <see cref="EmailSettings.Empty"/> si no existe.</summary>
    Task<ConfigurationReadResult<EmailSettings>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la configuración de correo en la fila única sin tocar el secreto cifrado ni su fecha de
    /// actualización. Una base inaccesible se informa en el resultado y no como excepción.
    /// </summary>
    Task<ConfigurationWriteResult> WriteAsync(EmailSettings settings, CancellationToken cancellationToken = default);
}
