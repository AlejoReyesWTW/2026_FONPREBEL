using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S6 y S7: la configuración de correo se guarda en la fila única de <c>EmailConfiguration</c> y la
/// escritura de la pantalla no toca el secreto cifrado ni su fecha de actualización.
/// </summary>
public sealed class EmailSettingsStoreTests
{
    private static readonly DateTime UpdatedUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reading_without_a_row_returns_the_empty_configuration()
    {
        using TestDatabase database = new();
        database.Migrate();
        EmailSettingsStore store = new(database.Factory, NullLogger<EmailSettingsStore>.Instance);

        ConfigurationReadResult<EmailSettings> result = await store.ReadAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(EmailSettings.Empty, result.Value);
    }

    [Fact]
    public async Task Writing_keeps_the_encrypted_secret_and_its_timestamp()
    {
        using TestDatabase database = new();
        database.Migrate();

        DpapiCredentialProvider provider = new();
        byte[] cipher = provider.Protect("contrasena-de-prueba").Value!;

        using (AppDbContext context = database.CreateContext())
        {
            context.EmailConfigurations.Add(new EmailConfiguration
            {
                Id = 1,
                PasswordCipher = cipher,
                PasswordUpdatedAtUtc = UpdatedUtc,
                UpdatedAtUtc = UpdatedUtc,
            });
            context.SaveChanges();
        }

        EmailSettingsStore store = new(database.Factory, NullLogger<EmailSettingsStore>.Instance);

        ConfigurationWriteResult written = await store.WriteAsync(
            new EmailSettings("smtp.ejemplo.test", 587, "StartTls", null, "seguros@ejemplo.test"));

        Assert.True(written.Succeeded);

        using AppDbContext reloaded = database.CreateContext();
        EmailConfiguration stored = Assert.Single(reloaded.EmailConfigurations);

        Assert.Equal("smtp.ejemplo.test", stored.Host);
        Assert.Equal(587, stored.Port);
        Assert.Equal("StartTls", stored.Security);
        Assert.Null(stored.Account);
        Assert.Equal("seguros@ejemplo.test", stored.SenderAddress);
        Assert.Equal(cipher, stored.PasswordCipher);
        Assert.Equal(UpdatedUtc, stored.PasswordUpdatedAtUtc);
    }

    [Fact]
    public async Task An_inaccessible_database_returns_a_typed_failure()
    {
        EmailSettingsStore store = new(new ThrowingContextFactory(), NullLogger<EmailSettingsStore>.Instance);

        ConfigurationReadResult<EmailSettings> read = await store.ReadAsync();
        ConfigurationWriteResult write = await store.WriteAsync(EmailSettings.Empty);

        Assert.Equal(ConfigurationStoreOutcome.StorageUnavailable, read.Outcome);
        Assert.Equal(ConfigurationStoreOutcome.StorageUnavailable, write.Outcome);
    }
}
