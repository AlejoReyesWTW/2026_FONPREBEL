using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// Anexo F.4 y F.9: registra y compara las huellas <c>SHA-256</c> de los archivos de entrada de una
/// ejecución. La identidad del insumo es su contenido, no su nombre ni su ubicación.
/// </summary>
/// <remarks>
/// <para>
/// Registrar una huella de un papel que ya existe en la ejecución la reemplaza, de modo que repetir la
/// captura no duplica filas (Anexo F.9).
/// </para>
/// <para>
/// Comparar no reescribe nada: sólo informa si el archivo coincide, cambió o no está disponible. Un archivo
/// ausente queda como no verificable y <b>nunca</b> como coincidencia (Anexo F.4 y F.5).
/// </para>
/// </remarks>
public sealed class InputFingerprintService : IInputFingerprintService
{
    private readonly IInputFingerprintStore _store;
    private readonly IFileFingerprintReader _reader;
    private readonly ILogger<InputFingerprintService> _logger;

    public InputFingerprintService(
        IInputFingerprintStore store,
        IFileFingerprintReader reader,
        ILogger<InputFingerprintService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _reader = reader;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> CaptureAsync(
        int executionId,
        IReadOnlyList<InputFileDescriptor> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            return BatchOperationResult.Failed(
                BatchOutcome.InvalidData,
                "La ejecución no tiene archivos de entrada que registrar.");
        }

        DateTime capturedAt = DateTime.UtcNow;
        List<ExecutionInputFingerprint> fingerprints = new(files.Count);

        foreach (InputFileDescriptor descriptor in files)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            if (!InputFileRole.IsKnown(descriptor.Role))
            {
                return BatchOperationResult.Failed(
                    BatchOutcome.InvalidData,
                    $"El papel '{descriptor.Role}' del archivo de entrada no está definido en el diseño de datos.");
            }

            FileFingerprintResult computed = await _reader
                .ComputeAsync(descriptor.FilePath, cancellationToken)
                .ConfigureAwait(false);

            if (!computed.Succeeded)
            {
                // Sin huella no hay insumo validado: no se registra nada y no se sigue (Anexo F.4).
                _logger.LogWarning(
                    "Ejecución {Ejecucion}: no se registró la huella del archivo {Ruta}. {Motivo}",
                    executionId,
                    descriptor.FilePath,
                    computed.Reason);

                return BatchOperationResult.Failed(
                    BatchOutcome.InputNotAvailable,
                    computed.Reason ?? "No se pudo calcular la huella de un archivo de entrada.");
            }

            FileFingerprint fingerprint = computed.Fingerprint!;

            fingerprints.Add(new ExecutionInputFingerprint
            {
                BatchExecutionId = executionId,
                Role = descriptor.Role,
                FilePath = fingerprint.FilePath,
                FileSizeBytes = fingerprint.SizeBytes,
                FileModifiedAtUtc = fingerprint.ModifiedAtUtc,
                Sha256 = fingerprint.Sha256,
                CapturedAtUtc = capturedAt,
            });
        }

        BatchOperationResult saved = await _store
            .SaveAsync(executionId, fingerprints, cancellationToken)
            .ConfigureAwait(false);

        if (saved.Succeeded)
        {
            _logger.LogInformation(
                "Ejecución {Ejecucion}: {Cantidad} huellas de archivos de entrada registradas",
                executionId,
                fingerprints.Count);
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult<IReadOnlyList<InputFingerprintComparison>>> VerifyAsync(
        int executionId,
        CancellationToken cancellationToken = default)
    {
        BatchOperationResult<IReadOnlyList<ExecutionInputFingerprint>> stored = await _store
            .ListAsync(executionId, cancellationToken)
            .ConfigureAwait(false);

        if (!stored.Succeeded)
        {
            return BatchOperationResult<IReadOnlyList<InputFingerprintComparison>>.Failed(
                stored.Outcome,
                stored.Detail);
        }

        List<InputFingerprintComparison> comparisons = new(stored.Value!.Count);

        foreach (ExecutionInputFingerprint fingerprint in stored.Value)
        {
            FileFingerprintResult computed = await _reader
                .ComputeAsync(fingerprint.FilePath, cancellationToken)
                .ConfigureAwait(false);

            comparisons.Add(Compare(fingerprint, computed));
        }

        return BatchOperationResult<IReadOnlyList<InputFingerprintComparison>>.Ok(comparisons);
    }

    /// <summary>
    /// Anexo F.4: la huella es el contenido. También se comparan el tamaño y la fecha de modificación, y
    /// cualquier diferencia bloquea la reanudación automática en lugar de aceptarse en silencio.
    /// </summary>
    private static InputFingerprintComparison Compare(
        ExecutionInputFingerprint stored,
        FileFingerprintResult computed)
    {
        if (computed.Status == FileReadStatus.Missing)
        {
            return new InputFingerprintComparison(
                stored.Role,
                stored.FilePath,
                InputFingerprintStatus.Unavailable,
                $"El archivo de entrada no está disponible en {stored.FilePath}: la huella no se puede verificar.");
        }

        if (computed.Status == FileReadStatus.Unreadable)
        {
            return new InputFingerprintComparison(
                stored.Role,
                stored.FilePath,
                InputFingerprintStatus.Unavailable,
                computed.Reason ?? $"No se pudo leer {stored.FilePath}: la huella no se puede verificar.");
        }

        FileFingerprint current = computed.Fingerprint!;

        if (!string.Equals(current.Sha256, stored.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new InputFingerprintComparison(
                stored.Role,
                stored.FilePath,
                InputFingerprintStatus.Modified,
                $"La huella de {stored.FilePath} cambió: el contenido no es el validado al iniciar la ejecución.");
        }

        if (current.SizeBytes != stored.FileSizeBytes)
        {
            return new InputFingerprintComparison(
                stored.Role,
                stored.FilePath,
                InputFingerprintStatus.Modified,
                $"El tamaño de {stored.FilePath} cambió de {stored.FileSizeBytes} a {current.SizeBytes} bytes.");
        }

        if (current.ModifiedAtUtc != stored.FileModifiedAtUtc)
        {
            return new InputFingerprintComparison(
                stored.Role,
                stored.FilePath,
                InputFingerprintStatus.Modified,
                $"La fecha de modificación de {stored.FilePath} cambió respecto de la registrada al iniciar la ejecución.");
        }

        return new InputFingerprintComparison(
            stored.Role,
            stored.FilePath,
            InputFingerprintStatus.Matches,
            "La huella coincide con la registrada al iniciar la ejecución.");
    }
}
