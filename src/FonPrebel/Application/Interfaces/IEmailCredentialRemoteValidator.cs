namespace FonPrebel.Application.Interfaces;

/// <summary>
/// §2.10.1 paso 2: resultado de validar una contraseña candidata contra el mecanismo real de
/// autenticación del correo. Los tres casos son los que el diseño de configuración ya documenta
/// (§3.2 de <c>docs/diseno/04-configuracion.md</c>).
/// </summary>
public enum RemoteCredentialValidation
{
    /// <summary>El servidor de correo aceptó la credencial.</summary>
    Accepted = 0,

    /// <summary>El servidor de correo rechazó la credencial: se conserva la anterior.</summary>
    Rejected = 1,

    /// <summary>
    /// El mecanismo no permite validar en ese momento. La aplicación no debe afirmar que la
    /// credencial quedó validada (§2.10.1).
    /// </summary>
    Unavailable = 2,
}

/// <summary>
/// §2.10.1 paso 2: punto de extensión para validar la contraseña nueva contra el mecanismo real de
/// autenticación del correo antes de reemplazar el secreto.
/// </summary>
/// <remarks>
/// Todavía no existe implementación ni registro en el contenedor porque el protocolo de correo del
/// banco está pendiente (§2.10): crear una hoy obligaría a inventar el mecanismo y el proxy, que es lo
/// que S23 prohíbe. La aplicación no llama a ningún servidor de correo desde acá. Cuando TI confirme
/// el protocolo, la implementación se registra en <c>AddFonPrebelSecurity</c> y se usa en
/// <see cref="IEmailCredentialService.TryReplaceAsync"/> después del paso 1 (comparación local de la
/// contraseña anterior) y antes de persistir: un <see cref="RemoteCredentialValidation.Rejected"/>
/// debe salir de ese método sin tocar la credencial anterior.
/// </remarks>
public interface IEmailCredentialRemoteValidator
{
    /// <summary>Valida la contraseña candidata contra el servidor de correo real.</summary>
    /// <param name="candidatePassword">Contraseña nueva que el analista quiere dejar configurada.</param>
    /// <param name="cancellationToken">Cancelación cooperativa de la validación.</param>
    Task<RemoteCredentialValidation> ValidateAsync(
        string candidatePassword,
        CancellationToken cancellationToken = default);
}
