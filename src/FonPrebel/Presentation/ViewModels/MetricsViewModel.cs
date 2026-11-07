using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;
using FonPrebel.Domain.Entities;
using FonPrebel.Presentation.ViewModels.Metrics;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Métricas (ficha V5, §2.22): indicadores acumulados e históricos calculados siempre desde los
/// registros de SQLite, filtrables por lote, con numerador y denominador visibles en los porcentajes.
/// </summary>
/// <remarks>
/// La pantalla no mantiene contadores propios: cada refresco lee los lotes una vez
/// (<see cref="IBatchStateQuery.ListBatchesAsync"/>), lee los destinatarios del alcance una vez por lote
/// (<see cref="IBatchRecipientService.ListAsync"/>) y arma las tarjetas con esos datos. La lectura corre en
/// segundo plano y se cancela al cerrar la vista, así la interfaz nunca se congela. Lo que la
/// especificación deja pendiente se muestra marcado como pendiente, nunca con un número inventado.
/// </remarks>
public sealed partial class MetricsViewModel : SectionViewModel, IDisposable
{
    private readonly IBatchStateQuery _stateQuery;
    private readonly IBatchRecipientService _recipientService;

    private CancellationTokenSource? _loadCancellation;
    private MetricsTotals _totals = MetricsTotals.Empty;
    private bool _suppressReload;
    private bool _hasLoaded;
    private bool _disposed;

    [ObservableProperty]
    private BatchFilterOption? _selectedOption;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _lastRefreshText = "Todavía no se leyeron los indicadores.";

    [ObservableProperty]
    private string _noticeText = string.Empty;

    [ObservableProperty]
    private bool _hasNotice;

    [ObservableProperty]
    private string _readErrorText = string.Empty;

    [ObservableProperty]
    private bool _hasReadError;

    public MetricsViewModel(IBatchStateQuery stateQuery, IBatchRecipientService recipientService)
        : base(
            "metrics",
            "Métricas",
            "Indicadores acumulados calculados desde la base local, filtrables por lote, con numerador y denominador visibles. Los indicadores que la especificación deja pendientes se muestran marcados como pendientes.")
    {
        ArgumentNullException.ThrowIfNull(stateQuery);
        ArgumentNullException.ThrowIfNull(recipientService);

        _stateQuery = stateQuery;
        _recipientService = recipientService;

        _selectedOption = BatchFilterOption.AllBatches;
        BatchOptions.Add(BatchFilterOption.AllBatches);

        // La pantalla nunca arranca vacía: primero se ven las tarjetas en cero y el refresco las completa.
        Apply(_totals);
    }

    /// <summary>Entradas del selector: "Todos los lotes" y un ítem por lote guardado.</summary>
    public ObservableCollection<BatchFilterOption> BatchOptions { get; } = new();

    /// <summary>Tarjetas del tablero, en orden de lectura.</summary>
    public ObservableCollection<MetricCard> Cards { get; } = new();

    /// <summary>Se llama al mostrarse la vista: vuelve a leer los indicadores desde la base.</summary>
    public void Activate()
    {
        if (_disposed)
        {
            return;
        }

        RefreshCommand.Execute(null);
    }

    /// <summary>Se llama al cerrarse la vista: cancela la lectura pendiente.</summary>
    public void Deactivate() => CancelPendingLoad();

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        CancelPendingLoad();
    }

    /// <summary>Vuelve a leer lotes, ejecuciones, destinatarios, envíos y PDFs, y rearma las tarjetas.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _loadCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        CancellationToken cancellationToken = cancellation.Token;
        BatchFilterOption scope = SelectedOption ?? BatchFilterOption.AllBatches;

        IsBusy = true;

        try
        {
            MetricsLoadResult result = await Task
                .Run(() => LoadAsync(scope, cancellationToken), cancellationToken)
                .ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ApplyOptions(result.Options);
            Apply(result.Totals);
            ReadErrorText = result.Totals.RecipientsComplete ? string.Empty : ExplainPartialRead(result);
            HasReadError = !result.Totals.RecipientsComplete;
            _hasLoaded = true;
            UpdateNotice();
            LastRefreshText = $"Actualizado a las {DateTime.Now:HH:mm:ss}.";
        }
        catch (OperationCanceledException)
        {
            // Un refresco nuevo o el cierre de la vista canceló esta lectura: no se pisa nada.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ReadErrorText = $"No se pudieron calcular los indicadores: {exception.Message}";
            HasReadError = true;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Lee la base una vez por consulta: lotes y ejecuciones en una sola lista, y destinatarios del alcance
    /// una vez por lote. No hay consulta por tarjeta ni contadores paralelos.
    /// </summary>
    private async Task<MetricsLoadResult> LoadAsync(BatchFilterOption scope, CancellationToken cancellationToken)
    {
        IReadOnlyList<BatchSummary> batches = await _stateQuery
            .ListBatchesAsync(cancellationToken)
            .ConfigureAwait(false);

        List<BatchSummary> inScope = scope.BatchId is int batchId
            ? batches.Where(batch => batch.BatchId == batchId).ToList()
            : batches.ToList();

        List<BatchRecipient> recipients = new();
        int failedReads = 0;
        string? failureDetail = null;

        foreach (BatchSummary batch in inScope)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BatchOperationResult<IReadOnlyList<BatchRecipient>> listed = await _recipientService
                .ListAsync(batch.BatchId, cancellationToken)
                .ConfigureAwait(false);

            if (listed.Succeeded && listed.Value is not null)
            {
                recipients.AddRange(listed.Value);
            }
            else
            {
                failedReads++;
                failureDetail ??= listed.Detail;
            }
        }

        return new MetricsLoadResult(
            BatchFilterOption.ComputeOptions(batches),
            MetricsTotals.From(inScope, recipients, failedReads),
            failureDetail);
    }

    /// <summary>Reemplaza las entradas del selector sin disparar una lectura por cada cambio.</summary>
    private void ApplyOptions(IReadOnlyList<BatchFilterOption> options)
    {
        int? previousBatchId = SelectedOption?.BatchId;

        _suppressReload = true;

        try
        {
            BatchOptions.Clear();

            foreach (BatchFilterOption option in options)
            {
                BatchOptions.Add(option);
            }

            BatchFilterOption selected = options.FirstOrDefault(option => option.BatchId == previousBatchId)
                ?? BatchFilterOption.AllBatches;

            SelectedOption = selected;
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private void Apply(MetricsTotals totals)
    {
        _totals = totals;

        IReadOnlyList<MetricCard> cards = MetricsCardBuilder.Build(totals, ScopeText());

        Cards.Clear();

        foreach (MetricCard card in cards)
        {
            Cards.Add(card);
        }
    }

    /// <summary>Textos del alcance para los rótulos: global o un lote concreto.</summary>
    private string ScopeText() =>
        SelectedOption is { BatchId: int } option && !string.IsNullOrWhiteSpace(option.Code)
            ? $"el lote {option.Code}"
            : "todos los lotes";

    /// <summary>Leyenda que explica por qué los contadores están en cero.</summary>
    private void UpdateNotice()
    {
        if (!_hasLoaded || HasReadError)
        {
            HasNotice = false;
            NoticeText = string.Empty;

            return;
        }

        if (_totals.BatchCount == 0)
        {
            NoticeText = "Todavía no hay lotes procesados: los contadores están en cero hasta que se registre el primer lote.";
        }
        else if (_totals.RecipientCount == 0 && SelectedOption?.BatchId is not null)
        {
            NoticeText = $"El lote {SelectedOption.Code} todavía no tiene destinatarios registrados: los contadores de envíos y PDFs están en cero.";
        }
        else if (_totals.RecipientCount == 0)
        {
            NoticeText = "Los lotes registrados todavía no tienen destinatarios: los contadores de envíos y PDFs están en cero.";
        }
        else
        {
            HasNotice = false;
            NoticeText = string.Empty;

            return;
        }

        HasNotice = true;
    }

    private static string ExplainPartialRead(MetricsLoadResult result)
    {
        string batches = result.Totals.FailedBatchReads == 1 ? "1 lote" : $"{result.Totals.FailedBatchReads} lotes";
        string detail = string.IsNullOrWhiteSpace(result.FailureDetail) ? string.Empty : $" {result.FailureDetail}";

        return $"No se pudieron leer los destinatarios de {batches}: los contadores de destinatarios, envíos y PDFs no están completos.{detail}";
    }

    private void CancelPendingLoad()
    {
        CancellationTokenSource? pending = Interlocked.Exchange(ref _loadCancellation, null);
        pending?.Cancel();
        pending?.Dispose();
    }

    partial void OnSelectedOptionChanged(BatchFilterOption? value)
    {
        if (_suppressReload || _disposed)
        {
            return;
        }

        RefreshCommand.Execute(null);
    }

    /// <summary>Resultado de una lectura: entradas del selector, contadores y, si falló, el motivo.</summary>
    private sealed record MetricsLoadResult(
        IReadOnlyList<BatchFilterOption> Options,
        MetricsTotals Totals,
        string? FailureDetail);
}
