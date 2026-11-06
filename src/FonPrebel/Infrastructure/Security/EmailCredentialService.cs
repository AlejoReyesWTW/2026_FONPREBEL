using System.Security.Cryptography;
using System.Text;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Security;

/// <summary>
/// S7 (§2.10.1): estado, comparación local y reemplazo atómico de la credencial de correo guardada en
/// <c>EmailConfiguration</c>. Cada operación abre su propio contexto desde la factoría: los contextos
/// son de vida corta y nunca se comparten entre hilos (S3).
/// </summary>
/// <remarks>
/// La contraseña guardada nunca sale de esta clase: no se devuelve, no se muestra y no se registra. El
/// único valor que viaja hacia afuera es el resultado tipado de cada operación.
/// </remarks>
public sealed class EmailCredentialService : IEmailCredentialService
{
    /// <summary>La tabla admite una sola fila y la restricción <c>CHECK (Id = 1)</c> la fija en 1.</summary>
    private const int EmailConfigurationId = 1;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ICredentialProvider _credentialProvider;
    private readonly ILogger<EmailCredentialService> _logger;

    public EmailCredentialService(
        IDbContextFactory<AppDbContext> contextFactory,
        ICredentialProvider credentialProvider,
        ILogger<EmailCredentialService> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(credentialProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<EmailCredentialStatus> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await using AppDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        EmailConfiguration? configuration = await FindAsync(context, cancellationToken).ConfigureAwait(false);
        EmailCredentialState state = ReadState(configuration);

        _logger.LogDebug("Estado de la credencial de correo: {State}", state);

        return new EmailCredentialStatus(state, configuration?.PasswordUpdatedAtUtc);
    }

    public async Task<bool> VerifyAsync(string candidate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        await using AppDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        EmailConfiguration? configuration = await FindAsync(context, cancellationToken).ConfigureAwait(false);
        if (configuration?.PasswordCipher is null || configuration.PasswordCipher.Length == 0)
        {
            _logger.LogWarning("No hay credencial de correo configurada para comparar la contraseña anterior.");

            return false;
        }

        // §2.10.1 paso 1: el descifrado y la comparación son locales; no se contacta ningún servidor.
        CredentialResult<string> stored = _credentialProvider.Unprotect(configuration.PasswordCipher);
        if (!stored.Succeeded)
        {
            _logger.LogWarning(NotRecoverableMessage);

            return false;
        }

        bool matches = AreEqualInConstantTime(stored.Value!, candidate);

        _logger.LogDebug(
            "Comparación local de la contraseña anterior: {Result}",
            matches ? "coincide" : "no coincide");

        return matches;
    }

    public async Task<CredentialReplaceResult> TryReplaceAsync(
        string previous,
        string newSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(newSecret))
        {
            _logger.LogWarning("Se rechazó el reemplazo de la credencial de correo: la contraseña nueva está vacía.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NewSecretRejected);
        }

        await using AppDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // §3.2: el reemplazo es atómico. La transacción y la única escritura de la fila hacen que un
        // fallo deje la credencial anterior intacta y la tabla con exactamente una fila.
        ReplaceScope? opened = await OpenReplaceScopeAsync(context, cancellationToken).ConfigureAwait(false);
        if (opened is null)
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        await using IDbContextTransaction transaction = opened.Transaction;
        EmailConfiguration? configuration = opened.Configuration;
        if (configuration?.PasswordCipher is null || configuration.PasswordCipher.Length == 0)
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.NotConfigured);
        }

        CredentialResult<string> stored = _credentialProvider.Unprotect(configuration.PasswordCipher);
        if (!stored.Succeeded)
        {
            _logger.LogWarning(NotRecoverableMessage);

            return new CredentialReplaceResult(CredentialReplaceOutcome.NotRecoverable);
        }

        if (!AreEqualInConstantTime(stored.Value!, previous ?? string.Empty))
        {
            _logger.LogWarning(
                "Se rechazó el reemplazo de la credencial de correo: la contraseña anterior no coincide con la guardada.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.PreviousPasswordMismatch);
        }

        // §2.10.1 paso 2 (fuera de alcance todavía): antes de persistir corresponde validar `newSecret`
        // contra el mecanismo real de autenticación del correo. El protocolo del banco está pendiente
        // (§2.10) y por eso acá no se llama a ningún servidor: cuando TI lo confirme, la dependencia
        // entra como IEmailCredentialRemoteValidator y un rechazo debe salir de este método sin tocar la
        // credencial anterior.
        CredentialResult<byte[]> replacement = _credentialProvider.Protect(newSecret);
        if (!replacement.Succeeded)
        {
            _logger.LogError(
                "No se pudo cifrar la contraseña nueva de la credencial de correo: se conservó la anterior.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NewSecretRejected);
        }

        DateTime now = DateTime.UtcNow;
        configuration.PasswordCipher = replacement.Value;
        configuration.PasswordUpdatedAtUtc = now;
        configuration.UpdatedAtUtc = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // La transacción se descarta sin confirmar: la credencial anterior sigue guardada.
            _logger.LogError(exception, "No se pudo guardar la credencial de correo: se conservó la anterior.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        _logger.LogInformation("Credencial de correo reemplazada; la anterior ya no es válida.");

        return new CredentialReplaceResult(CredentialReplaceOutcome.Replaced);
    }

    private const string NotRecoverableMessage =
        "La credencial de correo guardada no se puede descifrar en este perfil de Windows: hay que reingresarla.";

    /// <summary>Lo que necesita el reemplazo una vez abierta la transacción y leída la fila única.</summary>
    private sealed record ReplaceScope(EmailConfiguration? Configuration, IDbContextTransaction Transaction);

    /// <summary>
    /// Abre la transacción y lee la credencial. Una base inaccesible, ausente o sin migrar es un fallo
    /// de almacenamiento esperado y no una excepción que deba llegar a la UI: el método público
    /// devuelve el motivo y conserva la credencial anterior. Si la transacción llegó a abrirse y la
    /// lectura falla, se descarta para no dejar una transacción viva.
    /// </summary>
    private async Task<ReplaceScope?> OpenReplaceScopeAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;

        try
        {
            transaction = await context.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            EmailConfiguration? configuration = await FindAsync(context, cancellationToken).ConfigureAwait(false);

            return new ReplaceScope(configuration, transaction);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }

            _logger.LogError(
                exception,
                "No se pudo leer la credencial de correo: la base local no está accesible o no está migrada. Se conservaron los datos.");

            return null;
        }
    }

    private static Task<EmailConfiguration?> FindAsync(AppDbContext context, CancellationToken cancellationToken) =>
        context.EmailConfigurations.SingleOrDefaultAsync(
            configuration => configuration.Id == EmailConfigurationId,
            cancellationToken);

    private EmailCredentialState ReadState(EmailConfiguration? configuration)
    {
        if (configuration?.PasswordCipher is null || configuration.PasswordCipher.Length == 0)
        {
            return EmailCredentialState.NotConfigured;
        }

        return _credentialProvider.Unprotect(configuration.PasswordCipher).Succeeded
            ? EmailCredentialState.Available
            : EmailCredentialState.NotRecoverable;
    }

    /// <summary>
    /// Compara la contraseña guardada con la candidata en tiempo constante, sobre los bytes UTF-8, para
    /// no filtrar información por el tiempo de respuesta. Las diferencias de longitud no se consideran
    /// secretas.
    /// </summary>
    private static bool AreEqualInConstantTime(string expected, string candidate)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] candidateBytes = Encoding.UTF8.GetBytes(candidate);

        try
        {
            return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(candidateBytes);
        }
    }
}
