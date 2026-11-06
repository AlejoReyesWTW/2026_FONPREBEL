using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Infrastructure.Security;

/// <summary>
/// S7 (§2.10 y §2.10.1): las dos operaciones que permiten configurar la credencial cuando el reemplazo
/// con contraseña anterior no aplica. El alta cubre el primer uso y el reingreso cubre el cambio de
/// perfil de Windows, que hace inservible el secreto guardado. Ninguna de las dos borra filas ni toca
/// otra tabla: el historial operativo queda intacto.
/// </summary>
public sealed partial class EmailCredentialService
{
    public async Task<CredentialReplaceResult> TryRegisterAsync(
        string newSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(newSecret))
        {
            _logger.LogWarning("Se rechazó el alta de la credencial de correo: la contraseña está vacía.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NewSecretRejected);
        }

        await using AppDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        ReplaceScope? opened = await OpenReplaceScopeAsync(context, cancellationToken).ConfigureAwait(false);
        if (opened is null)
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        await using IDbContextTransaction transaction = opened.Transaction;
        EmailConfiguration? configuration = opened.Configuration;

        // El alta sólo aplica sin credencial guardada: con un secreto existente no se crea ni se
        // sobrescribe nada, ni siquiera cuando ese secreto no se puede descifrar (§3.2).
        if (configuration?.PasswordCipher is { Length: > 0 } storedCipher)
        {
            if (!_credentialProvider.Unprotect(storedCipher).Succeeded)
            {
                _logger.LogWarning(NotRecoverableMessage);

                return new CredentialReplaceResult(CredentialReplaceOutcome.NotRecoverable);
            }

            _logger.LogWarning(
                "Se rechazó el alta de la credencial de correo: ya hay una guardada y utilizable, así que corresponde reemplazarla con la contraseña anterior.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.AlreadyConfigured);
        }

        CredentialResult<byte[]> registration = _credentialProvider.Protect(newSecret);
        if (!registration.Succeeded)
        {
            _logger.LogError("No se pudo cifrar la credencial de correo: no se guardó nada.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NewSecretRejected);
        }

        // La fila única puede existir ya con los parámetros de correo sin secreto: en ese caso se le
        // agrega el secreto en lugar de insertar una segunda fila.
        EmailConfiguration target = configuration ?? new EmailConfiguration { Id = EmailConfigurationId };
        if (configuration is null)
        {
            context.EmailConfigurations.Add(target);
        }

        DateTime now = DateTime.UtcNow;
        target.PasswordCipher = registration.Value;
        target.PasswordUpdatedAtUtc = now;
        target.UpdatedAtUtc = now;

        if (!await SaveAndCommitAsync(
                context,
                transaction,
                "No se pudo registrar la credencial de correo: no se guardó nada.",
                cancellationToken).ConfigureAwait(false))
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        _logger.LogInformation(
            "Credencial de correo registrada por primera vez; queda guardada sin validar en el servidor.");

        return new CredentialReplaceResult(CredentialReplaceOutcome.Replaced);
    }

    public async Task<CredentialReplaceResult> TryReenterAsync(
        string newSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(newSecret))
        {
            _logger.LogWarning("Se rechazó el reingreso de la credencial de correo: la contraseña está vacía.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NewSecretRejected);
        }

        await using AppDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        ReplaceScope? opened = await OpenReplaceScopeAsync(context, cancellationToken).ConfigureAwait(false);
        if (opened is null)
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        await using IDbContextTransaction transaction = opened.Transaction;
        EmailConfiguration? configuration = opened.Configuration;

        if (configuration?.PasswordCipher is not { Length: > 0 } storedCipher)
        {
            _logger.LogWarning(
                "Se rechazó el reingreso de la credencial de correo: no hay ningún secreto guardado, así que corresponde el alta.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.NotConfigured);
        }

        // §3.2: saltarse la contraseña anterior sólo es válido cuando la guardada no se puede descifrar.
        if (_credentialProvider.Unprotect(storedCipher).Succeeded)
        {
            _logger.LogWarning(
                "Se rechazó el reingreso de la credencial de correo: la guardada se descifra en este perfil, así que el cambio exige la contraseña anterior.");

            return new CredentialReplaceResult(CredentialReplaceOutcome.PreviousPasswordRequired);
        }

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

        if (!await SaveAndCommitAsync(
                context,
                transaction,
                "No se pudo reingresar la credencial de correo: se conservó la anterior.",
                cancellationToken).ConfigureAwait(false))
        {
            return new CredentialReplaceResult(CredentialReplaceOutcome.StorageFailed);
        }

        _logger.LogInformation(
            "Credencial de correo no recuperable reemplazada por reingreso tras un cambio de perfil de Windows; la anterior ya no es válida.");

        return new CredentialReplaceResult(CredentialReplaceOutcome.Replaced);
    }
}
