using System.IO;
using System.Text;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using FonPrebel.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;

namespace FonPrebel.Tests.Security;

/// <summary>
/// S7 (§2.10.1): el alta de la primera credencial y el reingreso cuando la guardada no se descifra en
/// este perfil de Windows. Son las dos operaciones que el §2.10 exige para "volver a configurarla sin
/// perder el historial operativo": ninguna toca otra tabla ni borra filas, y con una credencial
/// utilizable se rechazan para no saltear la contraseña anterior.
/// </summary>
public sealed class EmailCredentialRegistrationTests : IDisposable
{
    /// <summary>Una credencial de prueba evidentemente ficticia: nunca se usa una contraseña real.</summary>
    private const string StoredSecret = "contrasena-de-prueba";

    private const string NewSecret = "contrasena-nueva-de-prueba";

    private static readonly DateTime InitialUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly DpapiCredentialProvider _provider = new();

    public EmailCredentialRegistrationTests() => _database.Migrate();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Register_creates_the_single_row_and_the_new_secret_verifies()
    {
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryRegisterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);
        Assert.True(result.Succeeded);

        EmailConfiguration stored = Assert.Single(ReadRows());
        Assert.Equal(1, stored.Id);
        Assert.NotNull(stored.PasswordCipher);
        Assert.NotEmpty(stored.PasswordCipher!);
        Assert.NotNull(stored.PasswordUpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.PasswordUpdatedAtUtc!.Value.Kind);

        // Ningún parámetro de correo se inventa: las columnas siguen nulas (§2.9.1).
        Assert.Null(stored.Host);
        Assert.Null(stored.Port);
        Assert.Null(stored.Security);
        Assert.Null(stored.SenderAddress);

        Assert.True(await service.VerifyAsync(NewSecret));
        Assert.False(await service.VerifyAsync(StoredSecret));
        Assert.Equal(EmailCredentialState.Available, (await service.GetStateAsync()).State);
    }

    [Fact]
    public async Task Register_with_an_available_credential_writes_nothing_and_reports_already_configured()
    {
        SeedSecret(StoredSecret);
        InstallWriteProbe();
        byte[] before = ReadCipher();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryRegisterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.AlreadyConfigured, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Empty(ReadProbeLog());

        EmailConfiguration stored = Assert.Single(ReadRows());
        Assert.True(before.SequenceEqual(stored.PasswordCipher!));
        Assert.Equal(InitialUtc, stored.PasswordUpdatedAtUtc);
        Assert.Equal(InitialUtc, stored.UpdatedAtUtc);

        Assert.True(await service.VerifyAsync(StoredSecret));
        Assert.False(await service.VerifyAsync(NewSecret));
    }

    [Fact]
    public async Task Register_with_a_not_recoverable_credential_writes_nothing_and_reports_not_recoverable()
    {
        SeedRaw(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        InstallWriteProbe();
        byte[] before = ReadCipher();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryRegisterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.NotRecoverable, result.Outcome);
        Assert.Empty(ReadProbeLog());
        Assert.True(before.SequenceEqual(ReadCipher()));
        Assert.Single(ReadRows());
        Assert.Equal(EmailCredentialState.NotRecoverable, (await service.GetStateAsync()).State);
    }

    [Fact]
    public async Task Register_uses_the_existing_email_row_and_keeps_its_settings()
    {
        SeedEmailSettingsOnly();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryRegisterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);

        EmailConfiguration stored = Assert.Single(ReadRows());
        Assert.Equal(1, stored.Id);
        Assert.Equal("smtp.ejemplo.test", stored.Host);
        Assert.Equal(587, stored.Port);
        Assert.True(await service.VerifyAsync(NewSecret));
    }

    [Fact]
    public async Task Reentry_replaces_the_undecryptable_cipher_and_leaves_one_row()
    {
        SeedRaw(new byte[] { 9, 8, 7, 6, 5 });
        InstallWriteProbe();
        byte[] before = ReadCipher();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryReenterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);
        Assert.True(result.Succeeded);

        // Sólo un UPDATE de la fila única: no hay INSERT, ni DELETE, ni tabla nueva.
        Assert.Equal(new[] { "UPDATE" }, ReadProbeLog());

        EmailConfiguration stored = Assert.Single(ReadRows());
        Assert.Equal(1, stored.Id);
        Assert.False(before.SequenceEqual(stored.PasswordCipher!));
        Assert.True(stored.PasswordUpdatedAtUtc > InitialUtc);
        Assert.True(stored.UpdatedAtUtc > InitialUtc);

        Assert.True(await service.VerifyAsync(NewSecret));
        Assert.False(await service.VerifyAsync(StoredSecret));
        Assert.Equal(EmailCredentialState.Available, (await service.GetStateAsync()).State);
    }

    [Fact]
    public async Task Reentry_keeps_the_email_settings_of_the_same_row()
    {
        SeedRaw(new byte[] { 4, 4, 4, 4 });
        SeedEmailSettingsOnly();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryReenterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);

        EmailConfiguration stored = Assert.Single(ReadRows());
        Assert.Equal("smtp.ejemplo.test", stored.Host);
        Assert.Equal(587, stored.Port);
        Assert.Equal("seguros@ejemplo.test", stored.SenderAddress);
    }

    [Fact]
    public async Task Reentry_with_an_available_credential_is_rejected()
    {
        SeedSecret(StoredSecret);
        InstallWriteProbe();
        byte[] before = ReadCipher();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryReenterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.PreviousPasswordRequired, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Empty(ReadProbeLog());
        Assert.True(before.SequenceEqual(ReadCipher()));

        Assert.True(await service.VerifyAsync(StoredSecret));
        Assert.False(await service.VerifyAsync(NewSecret));
    }

    [Fact]
    public async Task Reentry_without_a_stored_secret_reports_not_configured()
    {
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryReenterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.NotConfigured, result.Outcome);
        Assert.Empty(ReadRows());
    }

    [Fact]
    public async Task Registration_and_reentry_reject_an_empty_new_secret()
    {
        EmailCredentialService service = CreateService();

        Assert.Equal(CredentialReplaceOutcome.NewSecretRejected, (await service.TryRegisterAsync(string.Empty)).Outcome);
        Assert.Empty(ReadRows());

        SeedRaw(new byte[] { 7, 7, 7, 7 });
        byte[] before = ReadCipher();

        Assert.Equal(CredentialReplaceOutcome.NewSecretRejected, (await service.TryReenterAsync(string.Empty)).Outcome);
        Assert.True(before.SequenceEqual(ReadCipher()));
        Assert.Single(ReadRows());
    }

    [Fact]
    public async Task Reentry_is_logged_at_information_level_without_the_secret()
    {
        SeedRaw(new byte[] { 2, 4, 6, 8 });

        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Serilog.Core.Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        using (ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddSerilog(logger, dispose: false)))
        {
            EmailCredentialService service = CreateService(factory.CreateLogger<EmailCredentialService>());
            Assert.Equal(
                CredentialReplaceOutcome.Replaced,
                (await service.TryReenterAsync(NewSecret)).Outcome);
        }

        string log = File.ReadAllText(paths.ExecutionLogPath(today));
        string[] credentialLines = log
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("credencial", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        string line = Assert.Single(credentialLines);
        Assert.Contains(" | INFO | ", line, StringComparison.Ordinal);
        Assert.Contains("no recuperable", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("perfil", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(NewSecret, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_stores_no_plaintext_in_the_database_files()
    {
        CredentialReplaceResult result = await CreateService().TryRegisterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);

        SqliteConnection.ClearAllPools();

        byte[] databaseBytes = ReadDatabaseDirectoryBytes();

        Assert.False(ContainsText(databaseBytes, NewSecret));
    }

    [Fact]
    public async Task Reentry_stores_no_plaintext_in_the_database_files()
    {
        SeedRaw(new byte[] { 5, 5, 5, 5, 5 });

        CredentialReplaceResult result = await CreateService().TryReenterAsync(NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);

        SqliteConnection.ClearAllPools();

        byte[] databaseBytes = ReadDatabaseDirectoryBytes();

        Assert.False(ContainsText(databaseBytes, NewSecret));
        Assert.False(ContainsText(databaseBytes, StoredSecret));
    }

    private EmailCredentialService CreateService(ILogger<EmailCredentialService>? logger = null) =>
        new(_database.Factory, _provider, logger ?? NullLogger<EmailCredentialService>.Instance);

    private void SeedSecret(string secret) => SeedRaw(_provider.Protect(secret).Value!);

    private void SeedRaw(byte[] cipher)
    {
        using AppDbContext context = _database.CreateContext();
        context.EmailConfigurations.Add(new EmailConfiguration
        {
            Id = 1,
            PasswordCipher = cipher,
            PasswordUpdatedAtUtc = InitialUtc,
            UpdatedAtUtc = InitialUtc,
        });
        context.SaveChanges();
    }

    /// <summary>La fila única puede existir sólo con los parámetros de correo, sin secreto.</summary>
    private void SeedEmailSettingsOnly()
    {
        using AppDbContext context = _database.CreateContext();
        EmailConfiguration? configuration = context.EmailConfigurations
            .SingleOrDefault(row => row.Id == 1);

        if (configuration is null)
        {
            configuration = new EmailConfiguration { Id = 1 };
            context.EmailConfigurations.Add(configuration);
        }

        configuration.Host = "smtp.ejemplo.test";
        configuration.Port = 587;
        configuration.Security = "StartTls";
        configuration.SenderAddress = "seguros@ejemplo.test";
        configuration.UpdatedAtUtc = InitialUtc;
        context.SaveChanges();
    }

    private byte[] ReadCipher()
    {
        using AppDbContext context = _database.CreateContext();

        return context.EmailConfigurations.AsNoTracking().Single(row => row.Id == 1).PasswordCipher!;
    }

    private IReadOnlyList<EmailConfiguration> ReadRows()
    {
        using AppDbContext context = _database.CreateContext();

        return context.EmailConfigurations.AsNoTracking().ToList();
    }

    /// <summary>Disparadores que registran cada escritura de la tabla: prueban que un rechazo no escribe.</summary>
    private void InstallWriteProbe()
    {
        ExecuteRaw("CREATE TABLE probe_log (op TEXT NOT NULL)");
        ExecuteRaw("CREATE TRIGGER probe_upd AFTER UPDATE ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('UPDATE'); END");
        ExecuteRaw("CREATE TRIGGER probe_ins AFTER INSERT ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('INSERT'); END");
        ExecuteRaw("CREATE TRIGGER probe_del AFTER DELETE ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('DELETE'); END");
    }

    private IReadOnlyList<string> ReadProbeLog()
    {
        using SqliteConnection connection = new($"Data Source={_database.DatabasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT op FROM probe_log ORDER BY rowid";

        List<string> operations = new();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            operations.Add(reader.GetString(0));
        }

        return operations;
    }

    private void ExecuteRaw(string sql)
    {
        using SqliteConnection connection = new($"Data Source={_database.DatabasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private byte[] ReadDatabaseDirectoryBytes()
    {
        using MemoryStream buffer = new();
        foreach (string file in Directory.GetFiles(_database.Paths.DatabaseDirectory))
        {
            byte[] content = File.ReadAllBytes(file);
            buffer.Write(content, 0, content.Length);
        }

        return buffer.ToArray();
    }

    private static bool ContainsText(byte[] haystack, string text) =>
        ContainsSequence(haystack, Encoding.UTF8.GetBytes(text))
        || ContainsSequence(haystack, Encoding.Unicode.GetBytes(text));

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int start = 0; start <= haystack.Length - needle.Length; start++)
        {
            int offset = 0;
            while (offset < needle.Length && haystack[start + offset] == needle[offset])
            {
                offset++;
            }

            if (offset == needle.Length)
            {
                return true;
            }
        }

        return false;
    }
}
