using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// Arma los servicios de lotes, ejecuciones y destinatarios sobre una base temporal (sin tocar
/// <c>%LOCALAPPDATA%</c>) y una carpeta temporal de archivos, con la misma composición que usa la
/// aplicación. La factoría de contextos cuenta lo que se abre para poder verificar que cada operación usa
/// el suyo.
/// </summary>
internal sealed class BatchTestContext : IDisposable
{
    private readonly ServiceProvider _provider;

    public BatchTestContext()
    {
        Database = new TestDatabase();
        Database.Migrate();

        Files = new TempDirectory();
        Counting = new CountingContextFactory(Database.Factory);

        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddFonPrebelBatches();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(Counting);

        _provider = services.BuildServiceProvider();

        Batches = _provider.GetRequiredService<IBatchService>();
        Store = _provider.GetRequiredService<IBatchStore>();
        State = _provider.GetRequiredService<IBatchStateQuery>();
        Recipients = _provider.GetRequiredService<IBatchRecipientService>();
        Fingerprints = _provider.GetRequiredService<IInputFingerprintService>();
        Resume = _provider.GetRequiredService<IBatchResumeService>();
        Policy = _provider.GetRequiredService<RecipientProcessingPolicy>();
    }

    public TestDatabase Database { get; }

    public TempDirectory Files { get; }

    public CountingContextFactory Counting { get; }

    public IBatchService Batches { get; }

    public IBatchStore Store { get; }

    public IBatchStateQuery State { get; }

    public IBatchRecipientService Recipients { get; }

    public IInputFingerprintService Fingerprints { get; }

    public IBatchResumeService Resume { get; }

    public RecipientProcessingPolicy Policy { get; }

    public Task<BatchCreated> CreateAsync(int periodYear = 2026, int periodMonth = 11, string createdBy = "analista")
    {
        return CreateAsync(new BatchCreationRequest(periodYear, periodMonth, createdBy));
    }

    public async Task<BatchCreated> CreateAsync(BatchCreationRequest request)
    {
        BatchOperationResult<BatchCreated> result = await Batches.CreateAsync(request);

        Assert.True(result.Succeeded, $"El lote de prueba debe crearse: {result.Outcome} {result.Detail}");

        return result.Value!;
    }

    /// <summary>Escribe un archivo de entrada ficticio en la carpeta temporal y devuelve su ruta.</summary>
    public string WriteFile(string fileName, string content)
    {
        string path = Path.Combine(Files.Path, fileName);
        File.WriteAllText(path, content);

        return path;
    }

    /// <summary>Inicia la ejecución y registra la huella de los archivos indicados, como al arrancar el lote.</summary>
    public async Task StartWithFingerprintsAsync(BatchCreated created, params string[] files)
    {
        BatchOperationResult<ExecutionAppended> started = await Batches.StartExecutionAsync(created.ExecutionId);
        Assert.True(started.Succeeded, $"La ejecución debe iniciarse: {started.Outcome} {started.Detail}");

        List<InputFileDescriptor> descriptors = files
            .Select((file, index) => new InputFileDescriptor(
                index == 0 ? InputFileRole.AnalystFile : InputFileRole.PolicySource,
                file))
            .ToList();

        BatchOperationResult captured = await Fingerprints.CaptureAsync(created.ExecutionId, descriptors);
        Assert.True(captured.Succeeded, $"Las huellas deben registrarse: {captured.Outcome} {captured.Detail}");
    }

    /// <summary>Registra un jubilado de prueba y devuelve su cédula ya normalizada.</summary>
    public async Task<string> RegisterAsync(int batchId, string cedula, string? email = "jubilado@ejemplo.test")
    {
        BatchOperationResult result = await Recipients.RegisterAsync(
            batchId,
            new[] { new RecipientRegistration(cedula, "Jubilado de Prueba", email) });

        Assert.True(result.Succeeded, $"El destinatario debe registrarse: {result.Outcome} {result.Detail}");

        return Cedula.Normalize(cedula);
    }

    public void Dispose()
    {
        _provider.Dispose();
        Database.Dispose();
        Files.Dispose();
    }
}
