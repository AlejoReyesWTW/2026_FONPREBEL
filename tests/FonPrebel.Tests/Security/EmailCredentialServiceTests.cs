using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using FonPrebel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace FonPrebel.Tests.Security;

/// <summary>
/// S7 (§2.10.1): el estado de la credencial, la comparación local de la contraseña anterior y el
/// reemplazo atómico del secreto, con una sola fila en <c>EmailConfiguration</c> y sin secretos en la
/// bitácora.
/// </summary>
public sealed class EmailCredentialServiceTests : IDisposable
{
    /// <summary>Valores evidentemente ficticios: ninguna prueba usa una contraseña real.</summary>
    private const string StoredSecret = "contrasena-de-prueba";

    private const string NewSecret = "contrasena-nueva-de-prueba";

    private const string WrongSecret = "contrasena-anterior-equivocada";

    private static readonly DateTime InitialUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly DpapiCredentialProvider _provider = new();

    public EmailCredentialServiceTests() => _database.Migrate();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task State_without_a_row_is_not_configured()
    {
        EmailCredentialService service = CreateService();

        EmailCredentialStatus status = await service.GetStateAsync();

        Assert.Equal(EmailCredentialState.NotConfigured, status.State);
        Assert.Null(status.PasswordUpdatedAtUtc);
        Assert.Empty(ReadAllConfigurations());
    }

    [Fact]
    public async Task State_with_a_valid_secret_is_available()
    {
        SeedCredential(StoredSecret);
        EmailCredentialService service = CreateService();

        EmailCredentialStatus status = await service.GetStateAsync();

        Assert.Equal(EmailCredentialState.Available, status.State);
        Assert.Equal(InitialUtc, status.PasswordUpdatedAtUtc);
    }

    [Fact]
    public async Task State_with_an_undecryptable_secret_is_not_recoverable()
    {
        SeedCipher(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        EmailCredentialService service = CreateService();

        EmailCredentialStatus status = await service.GetStateAsync();

        Assert.Equal(EmailCredentialState.NotRecoverable, status.State);
        Assert.False(await service.VerifyAsync(StoredSecret));
    }

    [Fact]
    public async Task Verify_accepts_the_stored_password_and_rejects_another_one()
    {
        SeedCredential(StoredSecret);
        EmailCredentialService service = CreateService();

        Assert.True(await service.VerifyAsync(StoredSecret));
        Assert.False(await service.VerifyAsync(WrongSecret));
        Assert.False(await service.VerifyAsync(string.Empty));
    }

    [Fact]
    public async Task Replacing_with_a_wrong_previous_password_changes_nothing_and_reports_the_reason()
    {
        SeedCredential(StoredSecret);
        EmailCredentialService service = CreateService();
        EmailConfiguration before = ReadConfiguration();

        CredentialReplaceResult result = await service.TryReplaceAsync(WrongSecret, NewSecret);

        Assert.Equal(CredentialReplaceOutcome.PreviousPasswordMismatch, result.Outcome);
        Assert.False(result.Succeeded);

        EmailConfiguration after = ReadConfiguration();
        Assert.True(before.PasswordCipher!.SequenceEqual(after.PasswordCipher!));
        Assert.Equal(before.PasswordUpdatedAtUtc, after.PasswordUpdatedAtUtc);
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.False(await service.VerifyAsync(NewSecret));
        Assert.Single(ReadAllConfigurations());
    }

    [Fact]
    public async Task Replacing_with_the_previous_password_stores_only_the_new_secret()
    {
        SeedCredential(StoredSecret);
        EmailCredentialService service = CreateService();
        EmailConfiguration before = ReadConfiguration();

        CredentialReplaceResult result = await service.TryReplaceAsync(StoredSecret, NewSecret);

        Assert.Equal(CredentialReplaceOutcome.Replaced, result.Outcome);
        Assert.True(result.Succeeded);

        EmailConfiguration stored = Assert.Single(ReadAllConfigurations());
        Assert.Equal(1, stored.Id);
        Assert.NotNull(stored.PasswordCipher);
        Assert.False(stored.PasswordCipher!.SequenceEqual(before.PasswordCipher!));
        Assert.True(stored.PasswordUpdatedAtUtc > InitialUtc);
        Assert.True(stored.UpdatedAtUtc > InitialUtc);

        Assert.False(await service.VerifyAsync(StoredSecret));
        Assert.True(await service.VerifyAsync(NewSecret));
        Assert.Equal(EmailCredentialState.Available, (await service.GetStateAsync()).State);
    }

    [Fact]
    public async Task No_log_line_contains_the_secret()
    {
        SeedCredential(StoredSecret);

        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        using (ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddSerilog(logger, dispose: false)))
        {
            EmailCredentialService service = CreateService(factory.CreateLogger<EmailCredentialService>());

            await service.GetStateAsync();
            await service.VerifyAsync(WrongSecret);
            await service.TryReplaceAsync(WrongSecret, NewSecret);
            await service.TryReplaceAsync(StoredSecret, NewSecret);
        }

        string executionLog = File.ReadAllText(paths.ExecutionLogPath(today));

        Assert.DoesNotContain(StoredSecret, executionLog);
        Assert.DoesNotContain(NewSecret, executionLog);
        Assert.Contains("credencial", executionLog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_security_registration_resolves_the_provider_and_the_service()
    {
        using TempDirectory temp = new();
        AppPaths paths = new(temp.Path);

        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFonPrebelDatabase(paths);
        services.AddFonPrebelSecurity();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<DpapiCredentialProvider>(provider.GetRequiredService<ICredentialProvider>());
        Assert.IsType<EmailCredentialService>(provider.GetRequiredService<IEmailCredentialService>());
    }

    private EmailCredentialService CreateService(ILogger<EmailCredentialService>? logger = null) =>
        new(_database.Factory, _provider, logger ?? NullLogger<EmailCredentialService>.Instance);

    private void SeedCredential(string secret) => SeedCipher(_provider.Protect(secret).Value!);

    private void SeedCipher(byte[] cipher)
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

    private EmailConfiguration ReadConfiguration()
    {
        using AppDbContext context = _database.CreateContext();

        return context.EmailConfigurations
            .AsNoTracking()
            .Single(configuration => configuration.Id == 1);
    }

    private IReadOnlyList<EmailConfiguration> ReadAllConfigurations()
    {
        using AppDbContext context = _database.CreateContext();

        return context.EmailConfigurations.AsNoTracking().ToList();
    }
}
