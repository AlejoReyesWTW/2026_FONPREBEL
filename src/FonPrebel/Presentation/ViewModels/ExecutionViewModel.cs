using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Domain.Enums;
using FonPrebel.Domain.ValueObjects;
using FonPrebel.Presentation.ViewModels.Execution;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Ejecución (ficha V1 del diseño, §2.18): el lote activo, el indicador de las cinco fases, una
/// barra por fase con su contador real, la nota del progreso global pendiente y las tres acciones del
/// analista con el gate del Anexo D.
/// </summary>
/// <remarks>
/// <para>
/// Todos los datos salen del estado persistido: el lote activo y el reanudable se leen con
/// <see cref="IBatchStateQuery"/> y la validación de recuperación con <see cref="IBatchResumeService"/>.
/// No hay contadores propios ni datos inventados: sin lote la pantalla muestra el estado vacío, y una fase
/// sin <c>TotalCount</c> muestra "—" en lugar de un porcentaje.
/// </para>
/// <para>
/// La fórmula del progreso general está pendiente en el spec (§2.16, §5.6), así que la pantalla muestra un
/// texto explícito en vez de un porcentaje global calculado a mano.
/// </para>
/// <para>
/// La lectura corre en segundo plano y se cancela al cerrar la vista, así la interfaz nunca se congela ni
/// queda trabajo pendiente sobre una pantalla que ya no está.
/// </para>
/// </remarks>
public sealed partial class ExecutionViewModel : SectionViewModel, IDisposable
{
    /// <summary>
    /// Anexo D: aviso literal cuando la configuración obligatoria no está completa. El texto no se
    /// reescribe en la vista.
    /// </summary>
    public const string IncompleteConfigurationWarning =
        "Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso.";

    private readonly ConfigurationReadinessGate _gate;
    private readonly ExecutionStateReader? _reader;

    private CancellationTokenSource? _loadCancellation;
    private bool _disposed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasBatch;

    [ObservableProperty]
    private string _batchCode = string.Empty;

    [ObservableProperty]
    private string _periodText = string.Empty;

    [ObservableProperty]
    private string _batchStatusText = string.Empty;

    [ObservableProperty]
    private string _batchStatusColor = ExecutionLabels.RunningColor;

    [ObservableProperty]
    private string _startedAtText = string.Empty;

    [ObservableProperty]
    private string _executionText = string.Empty;

    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private bool _isResumeAvailable;

    [ObservableProperty]
    private string _resumeTargetText = string.Empty;

    [ObservableProperty]
    private string _lastRefreshText = "Todavía no se leyó el estado del lote.";

    [ObservableProperty]
    private string _noticeText = string.Empty;

    [ObservableProperty]
    private bool _hasNotice;

    [ObservableProperty]
    private InfoBarSeverity _noticeSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private string _readErrorText = string.Empty;

    [ObservableProperty]
    private bool _hasReadError;

    /// <summary>
    /// Sin origen de estado registrado la pantalla no consulta nada y queda en estado vacío. Es la forma que
    /// usan las verificaciones de la vista y del gate del Anexo D, que construyen el ViewModel sin base.
    /// </summary>
    public ExecutionViewModel(
        ConfigurationReadinessGate gate,
        IBatchStateQuery? stateQuery = null,
        IBatchResumeService? resumeService = null)
        : base(
            "execution",
            "Ejecución",
            "Inicia, reanuda y sigue el proceso mensual: lote activo, avance de las cinco fases y resultado de cada una.")
    {
        ArgumentNullException.ThrowIfNull(gate);

        _gate = gate;
        _reader = stateQuery is null ? null : new ExecutionStateReader(stateQuery, resumeService);
        _gate.Changed += OnReadinessChanged;

        BuildPhases(execution: null);
    }

    /// <summary>Las cinco fases, en el orden del §3 del spec, con su punto de estado y su barra.</summary>
    public ObservableCollection<ExecutionPhaseItem> Phases { get; } = new();

    /// <summary>Sin lote se muestra el estado vacío en lugar del encabezado y de las barras.</summary>
    public bool ShowsEmptyState => !HasBatch;

    /// <summary>Las barras por fase sólo existen cuando hay una ejecución de la que leer cifras.</summary>
    public bool ShowsPhaseBars => HasBatch;

    /// <summary>Estado vacío: no hay lote del que leer nada.</summary>
    public string EmptyStateTitle => ExecutionNotices.EmptyStateTitle;

    /// <summary>Qué va a aparecer en la pantalla cuando exista un lote.</summary>
    public string EmptyStateDetailText => ExecutionNotices.EmptyStateDetail;

    /// <summary>§2.16 y §5.6: la fórmula del progreso general está pendiente, así que no se inventa.</summary>
    public string GlobalProgressNotice => ExecutionNotices.GlobalProgressPending;

    /// <summary>
    /// S18 y Anexo D: el botón INICIAR sólo se habilita con toda la configuración obligatoria válida y
    /// sin lote en ejecución.
    /// </summary>
    public bool CanStart => _gate.IsConfigurationComplete && !IsBatchRunning;

    /// <summary>Anexo D: el aviso amarillo aparece mientras falte configuración obligatoria.</summary>
    public bool ShowsIncompleteConfigurationWarning => !_gate.IsConfigurationComplete;

    /// <summary>Texto literal del aviso del Anexo D, para enlazarlo en la vista.</summary>
    public string IncompleteConfigurationWarningText => IncompleteConfigurationWarning;

    /// <summary>Se llama al mostrarse la vista: lee el estado persistido del lote.</summary>
    public void Activate()
    {
        if (_disposed || _reader is null)
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
        _gate.Changed -= OnReadinessChanged;
        CancelPendingLoad();
    }

    /// <summary>Vuelve a leer el lote activo y el reanudable, y rearma las cinco fases.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_disposed || _reader is null)
        {
            return;
        }

        CancellationToken token = BeginLoad();
        IsBusy = true;

        try
        {
            ExecutionLoadResult result = await Task
                .Run(() => _reader.LoadAsync(token), token)
                .ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Apply(result);
            ReadErrorText = string.Empty;
            HasReadError = false;
            LastRefreshText = $"Actualizado a las {ExecutionLabels.Time(DateTime.Now)}.";
            HasNotice = false;
        }
        catch (OperationCanceledException)
        {
            // Un refresco nuevo o el cierre de la vista canceló esta lectura: no se pisa nada.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ReadErrorText = $"No se pudo leer el estado del lote: {exception.Message}";
            HasReadError = true;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>Vuelca el estado leído en la pantalla. Sin lote se limpia todo y se arman las fases vacías.</summary>
    private void Apply(ExecutionLoadResult result)
    {
        BatchStateSnapshot? batch = result.ActiveBatch;

        HasBatch = batch is not null;

        if (batch is null)
        {
            BatchCode = string.Empty;
            PeriodText = string.Empty;
            BatchStatusText = string.Empty;
            BatchStatusColor = ExecutionLabels.RunningColor;
            StartedAtText = string.Empty;
            ExecutionText = string.Empty;
            IsBatchRunning = false;
            BuildPhases(execution: null);
        }
        else
        {
            ExecutionSnapshot? last = batch.LastExecution;

            BatchCode = batch.Code;
            PeriodText = ExecutionLabels.Period(batch.PeriodYear, batch.PeriodMonth);
            BatchStatusText = ExecutionLabels.BatchStatusLabel(batch.Status);
            BatchStatusColor = ExecutionLabels.BatchStatusColor(batch.Status);
            StartedAtText = last is null
                ? $"Lote creado el {ExecutionLabels.Date(batch.CreatedAtUtc)}"
                : $"Inicio de la ejecución {last.Number}: {ExecutionLabels.Date(last.StartedAtUtc)}";
            ExecutionText = last is null
                ? "Sin ejecuciones registradas todavía."
                : $"Ejecución {last.Number} de {batch.Executions.Count}";
            IsBatchRunning = batch.Status == BatchStatus.EJECUTANDO
                || last?.Status == BatchStatus.EJECUTANDO;
            BuildPhases(last);
        }

        IsResumeAvailable = result.Resumable is not null;
        ResumeTargetText = result.Resumable is { } resumable
            ? $"Lote {resumable.BatchCode}, ejecución {resumable.Number}: {ExecutionLabels.BatchStatusLabel(resumable.Status)}."
            : string.Empty;

        OnPropertyChanged(nameof(ShowsEmptyState));
        OnPropertyChanged(nameof(ShowsPhaseBars));
    }

    /// <summary>Las cinco fases siempre se pintan; sin ejecución se leen "Sin lote".</summary>
    private void BuildPhases(ExecutionSnapshot? execution)
    {
        Phases.Clear();

        foreach (int number in PhaseCatalog.Numbers)
        {
            PhaseSnapshot? snapshot = execution?.Phases.FirstOrDefault(phase => phase.Phase == number);

            Phases.Add(execution is null
                ? ExecutionPhaseItem.WithoutBatch(number)
                : ExecutionPhaseItem.ForPhase(number, snapshot));
        }
    }

    /// <summary>Cancela la lectura anterior antes de empezar una nueva: nunca corren dos a la vez.</summary>
    private CancellationToken BeginLoad()
    {
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _loadCancellation, cancellation);

        previous?.Cancel();
        previous?.Dispose();

        return cancellation.Token;
    }

    private void CancelPendingLoad()
    {
        CancellationTokenSource? pending = Interlocked.Exchange(ref _loadCancellation, null);

        pending?.Cancel();
        pending?.Dispose();
    }

    private void OnReadinessChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(ShowsIncompleteConfigurationWarning));
        OnPropertyChanged(nameof(StartToolTip));
    }

    partial void OnIsBatchRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(StartToolTip));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CancelToolTip));
    }

    partial void OnIsResumeAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(ResumeToolTip));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanCancel));
    }
}
