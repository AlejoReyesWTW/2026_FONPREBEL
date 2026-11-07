using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Registro de lotes, ejecuciones y destinatarios (S8, S20): los almacenes de persistencia y los servicios
/// de aplicación que los gobiernan.
/// </summary>
/// <remarks>
/// Todos son sin estado entre operaciones —cada llamada abre su propio contexto de base— y viven como
/// singletons del host, igual que el resto de la infraestructura.
/// </remarks>
public static class BatchServiceCollectionExtensions
{
    /// <summary>Registra el ciclo de vida de lotes, la reanudación con huellas y las reglas de no duplicación.</summary>
    public static IServiceCollection AddFonPrebelBatches(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IBatchCodeGenerator, BatchCodeGenerator>();
        services.AddSingleton<IBatchStore, BatchStore>();
        services.AddSingleton<IBatchStateQuery, BatchStateQuery>();
        services.AddSingleton<IBatchRecipientStore, BatchRecipientStore>();
        services.AddSingleton<IInputFingerprintStore, InputFingerprintStore>();
        services.AddSingleton<IFileFingerprintReader, FileFingerprintReader>();

        services.AddSingleton<RecipientProcessingPolicy>();

        services.AddSingleton<IBatchService, BatchService>();
        services.AddSingleton<IBatchRecipientService, BatchRecipientService>();
        services.AddSingleton<IInputFingerprintService, InputFingerprintService>();
        services.AddSingleton<IBatchResumeService, BatchResumeService>();

        return services;
    }
}
