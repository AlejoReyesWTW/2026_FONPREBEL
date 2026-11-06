namespace FonPrebel.Application.Interfaces;

/// <summary>Estado de la credencial de correo guardada (S7, §3.3 del diseño de configuración).</summary>
public enum EmailCredentialState
{
    /// <summary>No hay contraseña guardada: la pantalla debe pedirla.</summary>
    NotConfigured = 0,

    /// <summary>Hay una contraseña guardada y se descifra en este perfil de Windows.</summary>
    Available = 1,

    /// <summary>
    /// Hay un contenido guardado que no se descifra en este perfil (§2.10): la pantalla debe ofrecer
    /// reingresar la credencial sin perder el historial operativo.
    /// </summary>
    NotRecoverable = 2,
}

/// <summary>Estado de la credencial de correo y, si existe, cuándo se guardó el secreto.</summary>
/// <param name="State">Estado del secreto guardado.</param>
/// <param name="PasswordUpdatedAtUtc">Momento del último cambio del secreto, en UTC.</param>
public sealed record EmailCredentialStatus(EmailCredentialState State, DateTime? PasswordUpdatedAtUtc);

/// <summary>Motivo del resultado de un intento de reemplazo del secreto de correo.</summary>
public enum CredentialReplaceOutcome
{
    /// <summary>El secreto se reemplazó y la credencial anterior ya no sirve.</summary>
    Replaced = 0,

    /// <summary>No hay credencial guardada que reemplazar.</summary>
    NotConfigured = 1,

    /// <summary>La credencial guardada no se descifra en este perfil; se debe reingresar.</summary>
    NotRecoverable = 2,

    /// <summary>La contraseña anterior no coincide con la guardada: no se cambió nada.</summary>
    PreviousPasswordMismatch = 3,

    /// <summary>La contraseña nueva está vacía o no se pudo cifrar: no se cambió nada.</summary>
    NewSecretRejected = 4,

    /// <summary>No se pudo guardar: la credencial anterior quedó intacta.</summary>
    StorageFailed = 5,
}

/// <summary>Resultado de <see cref="IEmailCredentialService.TryReplaceAsync"/>.</summary>
/// <param name="Outcome">Motivo del resultado.</param>
public sealed record CredentialReplaceResult(CredentialReplaceOutcome Outcome)
{
    /// <summary>Indica si el secreto quedó reemplazado.</summary>
    public bool Succeeded => Outcome == CredentialReplaceOutcome.Replaced;
}

/// <summary>
/// S7 (§2.10.1): credencial de correo guardada cifrada con DPAPI. Sólo expone el estado y dos
/// operaciones: comparar candidatamente la contraseña guardada y reemplazarla de forma atómica. La
/// contraseña guardada nunca se devuelve ni se muestra.
/// </summary>
public interface IEmailCredentialService
{
    /// <summary>
    /// Indica si hay credencial configurada y si se puede descifrar en este perfil. Cada llamada usa su
    /// propio contexto de base.
    /// </summary>
    Task<EmailCredentialStatus> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// §2.10.1 paso 1: descifra la credencial guardada en memoria y la compara localmente con la
    /// candidata, sin llamar a ningún servidor. Devuelve <c>true</c> sólo si coincide.
    /// </summary>
    /// <param name="candidate">Contraseña anterior que ingresa el analista en el modo edición.</param>
    Task<bool> VerifyAsync(string candidate, CancellationToken cancellationToken = default);

    /// <summary>
    /// §2.10.1: reemplaza el secreto si <paramref name="previous"/> coincide con el guardado. Es
    /// atómico: ante cualquier fallo la credencial anterior queda intacta y la tabla
    /// <c>EmailConfiguration</c> sigue con exactamente una fila. El resultado informa el motivo y
    /// nunca incluye el secreto.
    /// </summary>
    /// <param name="previous">Contraseña anterior que ingresa el analista.</param>
    /// <param name="newSecret">Contraseña nueva que se va a cifrar y guardar.</param>
    /// <remarks>
    /// §2.10.1 paso 2 —validar la contraseña nueva contra el mecanismo real de autenticación del
    /// correo— queda fuera de esta implementación: el protocolo del banco está pendiente (§2.10). El
    /// punto de extensión es <see cref="IEmailCredentialRemoteValidator"/> y todavía no se llama a
    /// ningún servidor; por eso el reemplazo no se informa como validado en el servidor.
    /// </remarks>
    Task<CredentialReplaceResult> TryReplaceAsync(
        string previous,
        string newSecret,
        CancellationToken cancellationToken = default);
}
