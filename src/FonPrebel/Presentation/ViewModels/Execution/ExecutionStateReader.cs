using FonPrebel.Application.Interfaces;

namespace FonPrebel.Presentation.ViewModels.Execution;

/// <summary>
/// Lee del estado persistido lo que la pantalla Ejecución necesita: el lote activo, el lote reanudable y,
/// cuando se pide, la validación de recuperación (Anexo F.7).
/// </summary>
/// <remarks>
/// No guarda nada entre llamadas ni mantiene contadores: cada lectura abre su propio contexto de base a
/// través de <see cref="IBatchStateQuery"/>, así la consulta corre en segundo plano y la interfaz no se
/// congela. La validación nunca reanuda el proceso; sólo deja el lote en el estado que el servicio decide.
/// </remarks>
internal sealed class ExecutionStateReader
{
    private readonly IBatchStateQuery _stateQuery;
    private readonly IBatchResumeService? _resumeService;

    internal ExecutionStateReader(IBatchStateQuery stateQuery, IBatchResumeService? resumeService)
    {
        ArgumentNullException.ThrowIfNull(stateQuery);

        _stateQuery = stateQuery;
        _resumeService = resumeService;
    }

    /// <summary>Indica si hay servicio de reanudación para validar la recuperación.</summary>
    internal bool CanValidateRecovery => _resumeService is not null;

    /// <summary>Lote activo (el más reciente no terminal) y, si lo hay, el lote reanudable.</summary>
    internal async Task<ExecutionLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        BatchStateSnapshot? active = await _stateQuery
            .GetActiveBatchAsync(cancellationToken)
            .ConfigureAwait(false);

        RecoverableExecution? resumable = await _stateQuery
            .FindResumableAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ExecutionLoadResult(active, resumable);
    }

    /// <summary>Valida la recuperación y vuelve a leer el estado, en ese orden y sin reanudar nada.</summary>
    internal async Task<ResumeRun> ValidateThenLoadAsync(CancellationToken cancellationToken)
    {
        if (_resumeService is null)
        {
            throw new InvalidOperationException(
                "La pantalla se construyó sin servicio de reanudación: no hay validación de recuperación que ejecutar.");
        }

        BatchResumeValidationResult validation = await _resumeService
            .ValidateAsync(cancellationToken)
            .ConfigureAwait(false);

        ExecutionLoadResult state = await LoadAsync(cancellationToken).ConfigureAwait(false);

        return new ResumeRun(validation, state);
    }
}

/// <summary>Estado leído de la base: el lote activo y, si lo hay, el reanudable.</summary>
internal sealed record ExecutionLoadResult(BatchStateSnapshot? ActiveBatch, RecoverableExecution? Resumable);

/// <summary>Validación de recuperación y el estado posterior, para pintar las dos cosas juntas.</summary>
internal sealed record ResumeRun(BatchResumeValidationResult Validation, ExecutionLoadResult State);
