using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// S8 (§2.8): ciclo de vida de los lotes y de sus ejecuciones. Crear un lote crea su ejecución 1 con las
/// cinco fases en <see cref="PhaseStatus.PENDIENTE"/>; reanudar agrega una ejecución del mismo lote y nunca
/// un lote nuevo (§2.8 regla 5).
/// </summary>
/// <remarks>
/// La persistencia queda del lado de <see cref="IBatchStore"/>, que abre un contexto por operación y resuelve
/// en una sola transacción los cambios que tocan el lote y su ejecución a la vez.
/// </remarks>
public sealed class BatchService : IBatchService
{
    /// <summary>Intentos de generar un identificador libre antes de darse por vencido (§2.8 regla 1).</summary>
    public const int MaxCodeAttempts = 3;

    private readonly IBatchStore _store;
    private readonly IBatchStateQuery _stateQuery;
    private readonly IBatchCodeGenerator _codeGenerator;
    private readonly ILogger<BatchService> _logger;

    public BatchService(
        IBatchStore store,
        IBatchStateQuery stateQuery,
        IBatchCodeGenerator codeGenerator,
        ILogger<BatchService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(stateQuery);
        ArgumentNullException.ThrowIfNull(codeGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _stateQuery = stateQuery;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<BatchCreated>> CreateAsync(
        BatchCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PeriodYear is < 1 or > 9999 || request.PeriodMonth is < 1 or > 12)
        {
            return BatchOperationResult<BatchCreated>.Failed(
                BatchOutcome.InvalidPeriod,
                "El periodo del lote necesita un año de cuatro dígitos y un mes de 1 a 12.");
        }

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            return BatchOperationResult<BatchCreated>.Failed(
                BatchOutcome.InvalidData,
                "El lote se registra con el usuario de Windows que lo abrió.");
        }

        for (int attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            int sequence = await NextSequenceAsync(request.PeriodYear, request.PeriodMonth, cancellationToken)
                .ConfigureAwait(false);

            string code = _codeGenerator.Generate(request.PeriodYear, request.PeriodMonth, sequence);
            BatchOperationResult<BatchCreated> stored = await _store
                .AddBatchAsync(BuildBatch(code, request), cancellationToken)
                .ConfigureAwait(false);

            if (stored.Succeeded || stored.Outcome != BatchOutcome.DuplicateCode)
            {
                if (stored.Succeeded)
                {
                    _logger.LogInformation(
                        "Lote {Codigo} creado para el periodo {Anio}-{Mes} con la ejecución 1",
                        stored.Value!.Code,
                        request.PeriodYear,
                        request.PeriodMonth);
                }

                return stored;
            }
        }

        return BatchOperationResult<BatchCreated>.Failed(
            BatchOutcome.DuplicateCode,
            "No se pudo generar un identificador de lote libre para el periodo: no se creó ningún lote.");
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<ExecutionAppended>> StartExecutionAsync(
        int executionId,
        CancellationToken cancellationToken = default)
    {
        BatchOperationResult<ExecutionAppended> started = await _store
            .StartExecutionAsync(executionId, cancellationToken)
            .ConfigureAwait(false);

        if (started.Succeeded)
        {
            _logger.LogInformation(
                "Lote {Lote}: ejecución {Ejecucion} iniciada",
                started.Value!.BatchId,
                executionId);
        }

        return started;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<ExecutionAppended>> ResumeAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        BatchStateSnapshot? batch = await _stateQuery.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);

        if (batch is null)
        {
            return BatchOperationResult<ExecutionAppended>.Failed(
                BatchOutcome.BatchNotFound,
                $"No existe el lote {batchId}: no se reanuda nada.");
        }

        if (batch.Status != BatchStatus.LISTO_PARA_REANUDAR)
        {
            if (BatchStatusRules.IsTerminal(batch.Status))
            {
                return Rejected(
                    $"El lote {batch.Code} está cerrado como {batch.Status}: un lote terminal no se reanuda " +
                    "(Anexo F.8).");
            }

            return Rejected(
                $"El lote {batch.Code} está en estado {batch.Status} y sólo se reanuda después de que la " +
                "validación de huellas lo deje LISTO_PARA_REANUDAR (Anexo F.8).");
        }

        // La condición de reanudable se revalida acá y no se delega en una validación previa: sólo se reanuda
        // si la ÚLTIMA ejecución del lote es la interrumpida (Anexo F.7).
        RecoverableExecution? resumable = await _stateQuery
            .FindResumableAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resumable is null
            || resumable.BatchId != batchId
            || resumable.ExecutionId != batch.LastExecution?.ExecutionId)
        {
            return Rejected(
                $"La última ejecución del lote {batch.Code} no está en un estado recuperable: no se reanuda " +
                "(Anexo F.7).");
        }

        BatchOperationResult<ExecutionAppended> resumed = await _store
            .AddResumeExecutionAsync(batchId, cancellationToken)
            .ConfigureAwait(false);

        if (resumed.Succeeded)
        {
            _logger.LogInformation(
                "Lote {Codigo} reanudado con la ejecución {Numero} a partir de la ejecución {Anterior}",
                batch.Code,
                resumed.Value!.ExecutionNumber,
                resumed.Value.ResumedFromExecutionId);
        }

        return resumed;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> CloseExecutionAsync(
        int executionId,
        BatchStatus terminalStatus,
        CancellationToken cancellationToken = default)
    {
        if (terminalStatus is not (BatchStatus.COMPLETADO or BatchStatus.CANCELADO))
        {
            return BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "Una ejecución sólo se cierra como COMPLETADO o CANCELADO (Anexo F.8).");
        }

        BatchOperationResult closed = await _store
            .CloseExecutionAsync(executionId, terminalStatus, cancellationToken)
            .ConfigureAwait(false);

        if (closed.Succeeded)
        {
            _logger.LogInformation("Ejecución {Ejecucion} cerrada como {Estado}", executionId, terminalStatus);
        }

        return closed;
    }

    /// <inheritdoc />
    public Task<BatchOperationResult> SetStateAsync(
        int executionId,
        BatchStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return Task.FromResult(BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "El estado indicado no corresponde a ningún estado del diseño (Anexo F.8)."));
        }

        return _store.SetStateAsync(executionId, status, cancellationToken);
    }

    /// <summary>
    /// Rechazo de una reanudación por el estado del lote o de su última ejecución (Anexo F.7 y F.8).
    /// </summary>
    private static BatchOperationResult<ExecutionAppended> Rejected(string detail) =>
        BatchOperationResult<ExecutionAppended>.Failed(BatchOutcome.InvalidExecutionState, detail);

    /// <summary>
    /// Siguiente secuencia del periodo: el mayor identificador ya usado más uno. El formato lo lee el
    /// generador, así que acá no hay ninguna suposición sobre cómo se escribe el código.
    /// </summary>
    private async Task<int> NextSequenceAsync(int periodYear, int periodMonth, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> codes = await _store
            .ListBatchCodesAsync(periodYear, periodMonth, cancellationToken)
            .ConfigureAwait(false);

        int next = 1;

        foreach (string code in codes)
        {
            if (_codeGenerator.TryReadSequence(code, periodYear, periodMonth, out int sequence) && sequence >= next)
            {
                next = sequence + 1;
            }
        }

        return next;
    }

    private static Batch BuildBatch(string code, BatchCreationRequest request)
    {
        DateTime now = DateTime.UtcNow;

        Batch batch = new()
        {
            Code = code,
            PeriodYear = request.PeriodYear,
            PeriodMonth = request.PeriodMonth,
            Status = BatchStatus.LISTO,
            CreatedAtUtc = now,
            CreatedBy = request.CreatedBy.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        batch.Executions.Add(new BatchExecution
        {
            Number = 1,
            Status = BatchStatus.LISTO,
            StartedAtUtc = now,
            Phases = PhaseCatalog.CreatePending(),
        });

        return batch;
    }
}
