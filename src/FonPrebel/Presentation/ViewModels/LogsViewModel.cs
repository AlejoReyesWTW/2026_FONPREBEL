using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>
/// Sección Logs (V3): lee las dos bitácoras del día desde el archivo y las muestra sin salir de la
/// aplicación. La lectura ocurre en segundo plano, tolera archivos que todavía no existen y nunca
/// toca la base de datos (S17).
/// </summary>
public sealed partial class LogsViewModel : SectionViewModel
{
    /// <summary>Cantidad máxima de líneas que se leen de cada bitácora.</summary>
    public const int MaximumLinesPerLog = 2000;

    private readonly AppPaths _paths;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDayText))]
    private DateOnly _selectedDay;

    [ObservableProperty]
    private string _executionLogText = string.Empty;

    [ObservableProperty]
    private string _errorLogText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private int _executionLineCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private int _errorLineCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasReadError;

    [ObservableProperty]
    private string _readErrorText = string.Empty;

    public LogsViewModel(AppPaths paths)
        : base("logs", "Logs",
               "Dos paneles, execution.log y errors.log, con fecha, hora, nivel, lote, ejecución, fase y resultado. Los logs van a archivo, nunca a la base.")
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
        _selectedDay = DateOnly.FromDateTime(DateTime.Today);

        RefreshCommand.Execute(null);
    }

    /// <summary>Día seleccionado en el formato de la carpeta de bitácoras.</summary>
    public string SelectedDayText => SelectedDay.ToString(AppPaths.DayFolderFormat, CultureInfo.InvariantCulture);

    /// <summary>Verdadero cuando el día no tiene actividad y no hubo error de lectura.</summary>
    public bool IsEmpty => !HasReadError && !IsBusy && ExecutionLineCount == 0 && ErrorLineCount == 0;

    /// <summary>Vuelve a leer ambas bitácoras del día seleccionado.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        DateOnly day = SelectedDay;
        string executionPath = _paths.ExecutionLogPath(day);
        string errorPath = _paths.ErrorLogPath(day);

        IsBusy = true;

        try
        {
            (IReadOnlyList<string> execution, IReadOnlyList<string> errors) = await Task.Run(() =>
                (LogFileReader.ReadLastLines(executionPath, MaximumLinesPerLog),
                 LogFileReader.ReadLastLines(errorPath, MaximumLinesPerLog)));

            ExecutionLogText = string.Join(Environment.NewLine, execution);
            ErrorLogText = string.Join(Environment.NewLine, errors);
            ExecutionLineCount = execution.Count;
            ErrorLineCount = errors.Count;
            ReadErrorText = string.Empty;
            HasReadError = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ExecutionLogText = string.Empty;
            ErrorLogText = string.Empty;
            ExecutionLineCount = 0;
            ErrorLineCount = 0;
            ReadErrorText = $"No se pudo leer la bitácora del día: {exception.Message}";
            HasReadError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedDayChanged(DateOnly value)
    {
        RefreshCommand.Execute(null);
    }
}
