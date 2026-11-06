using System.IO;
using System.Text;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Security;

/// <summary>
/// Sondas de regresión de A4 (S7 y S23) derivadas de la verificación independiente de seguridad.
/// Usan disparadores de SQLite para observar desde afuera qué operación llega a la base: es la única
/// forma de probar que un reemplazo es un UPDATE de la fila única y no un INSERT o un DELETE.
/// </summary>
public sealed class EmailCredentialSecurityProbes : IDisposable
{
    private const string OldSecret = "contrasena-de-prueba";
    private const string NewSecret = "contrasena-nueva-de-prueba";
    private static readonly DateTime InitialUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly DpapiCredentialProvider _provider = new();

    public EmailCredentialSecurityProbes() => _database.Migrate();

    public void Dispose() => _database.Dispose();

    [Fact] // S7: el reemplazo es un UPDATE de la fila 1 dentro de una transacción; nunca INSERT ni DELETE.
    public async Task Replace_is_a_single_update_of_row_one()
    {
        Seed(OldSecret);
        ExecuteRaw("CREATE TABLE probe_log (op TEXT NOT NULL)");
        ExecuteRaw("CREATE TRIGGER probe_upd AFTER UPDATE ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('UPDATE'); END");
        ExecuteRaw("CREATE TRIGGER probe_ins AFTER INSERT ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('INSERT'); END");
        ExecuteRaw("CREATE TRIGGER probe_del AFTER DELETE ON EmailConfiguration BEGIN INSERT INTO probe_log VALUES ('DELETE'); END");

        CredentialReplaceResult result = await CreateService().TryReplaceAsync(OldSecret, NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);
        Assert.Equal(new[] { "UPDATE" }, ReadProbeLog());
        Assert.Equal(1, Assert.Single(ReadRows()).Id);
    }

    [Fact] // S7: si el guardado falla, la credencial anterior queda intacta y la tabla con una sola fila.
    public async Task Storage_failure_keeps_the_previous_credential()
    {
        Seed(OldSecret);
        byte[] before = ReadCipher();

        ExecuteRaw("CREATE TRIGGER probe_block_update BEFORE UPDATE ON EmailConfiguration BEGIN SELECT RAISE(ABORT, 'bloqueo de prueba'); END");
        CredentialReplaceResult result;
        try
        {
            result = await CreateService().TryReplaceAsync(OldSecret, NewSecret);
        }
        finally
        {
            ExecuteRaw("DROP TRIGGER IF EXISTS probe_block_update");
        }

        Assert.Equal(CredentialReplaceOutcome.StorageFailed, result.Outcome);
        Assert.True(before.SequenceEqual(ReadCipher()));
        Assert.Single(ReadRows());
        Assert.False(await CreateService().VerifyAsync(NewSecret));
    }

    [Fact] // S7 (§2.10): credencial no descifrable (otro perfil de Windows) => resultado tipado y fila intacta.
    public async Task Undecryptable_stored_secret_returns_not_recoverable_and_keeps_the_row()
    {
        SeedRaw(new byte[] { 9, 8, 7, 6, 5 });
        byte[] before = ReadCipher();
        EmailCredentialService service = CreateService();

        CredentialReplaceResult result = await service.TryReplaceAsync(OldSecret, NewSecret);

        Assert.Equal(CredentialReplaceOutcome.NotRecoverable, result.Outcome);
        Assert.Equal(EmailCredentialState.NotRecoverable, (await service.GetStateAsync()).State);
        Assert.True(before.SequenceEqual(ReadCipher()));
        Assert.Single(ReadRows());
    }

    [Fact] // S7: ni el secreto anterior ni el nuevo aparecen en claro en los archivos de la base.
    public async Task Successful_replace_stores_no_plaintext_in_the_database()
    {
        Seed(OldSecret);

        CredentialReplaceResult result = await CreateService().TryReplaceAsync(OldSecret, NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);

        SqliteConnection.ClearAllPools();
        byte[] databaseBytes = ReadDatabaseDirectoryBytes();

        Assert.False(ContainsText(databaseBytes, OldSecret));
        Assert.False(ContainsText(databaseBytes, NewSecret));
    }

    [Fact] // S3/S7: cada operación crea su propio contexto desde la factoría; ninguno se comparte.
    public async Task Each_operation_creates_its_own_context()
    {
        Seed(OldSecret);
        CountingFactory factory = new(_database);
        EmailCredentialService service = new(factory, _provider, NullLogger<EmailCredentialService>.Instance);

        await service.GetStateAsync();
        await service.VerifyAsync(OldSecret);
        await service.TryReplaceAsync(OldSecret, NewSecret);

        Assert.Equal(3, factory.Created);
    }

    [Fact] // S23: el validador remoto está declarado pero no implementado ni registrado (protocolo pendiente).
    public void Remote_validator_has_no_implementation_and_is_not_registered()
    {
        Type validator = typeof(IEmailCredentialRemoteValidator);

        Assert.DoesNotContain(
            validator.Assembly.GetTypes(),
            type => type != validator && !type.IsInterface && !type.IsAbstract && validator.IsAssignableFrom(type));

        using TempDirectory temp = new();
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFonPrebelDatabase(new AppPaths(temp.Path));
        services.AddFonPrebelSecurity();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IEmailCredentialRemoteValidator>());
    }

    private EmailCredentialService CreateService() =>
        new(_database.Factory, _provider, NullLogger<EmailCredentialService>.Instance);

    private void Seed(string secret) => SeedRaw(_provider.Protect(secret).Value!);

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

    private void ExecuteRaw(string sql)
    {
        using SqliteConnection connection = new($"Data Source={_database.DatabasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
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

    private sealed class CountingFactory : IDbContextFactory<AppDbContext>
    {
        private readonly TestDatabase _database;

        public CountingFactory(TestDatabase database) => _database = database;

        public int Created { get; private set; }

        public AppDbContext CreateDbContext()
        {
            Created++;

            return _database.CreateContext();
        }
    }
}
