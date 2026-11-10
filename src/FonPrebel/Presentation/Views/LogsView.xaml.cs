using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FonPrebel.Presentation.ViewModels;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Sección Logs (V3). Además de mostrar las dos bitácoras, mantiene la vista pegada al último registro:
/// cuando llega contenido nuevo, ambas barras se desplazan al final, para no tener que arrastrar el
/// scroll a mano cada vez.
/// </summary>
public partial class LogsView : UserControl
{
    private INotifyPropertyChanged? _observed;

    public LogsView()
    {
        InitializeComponent();

        Loaded += (_, _) => AttachToViewModel(DataContext);
        DataContextChanged += (_, eventArgs) => AttachToViewModel(eventArgs.NewValue);
    }

    private void AttachToViewModel(object? dataContext)
    {
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnViewModelPropertyChanged;
            _observed = null;
        }

        if (dataContext is INotifyPropertyChanged observed)
        {
            observed.PropertyChanged += OnViewModelPropertyChanged;
            _observed = observed;
        }

        ScrollToNewest();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(LogsViewModel.ExecutionLogText) or nameof(LogsViewModel.ErrorLogText))
        {
            ScrollToNewest();
        }
    }

    /// <summary>
    /// Pide el desplazamiento en segundo plano del despachador para que el texto nuevo ya esté medido
    /// cuando se calcula el final del contenido.
    /// </summary>
    private void ScrollToNewest() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                ExecutionLogScroll.ScrollToEnd();
                ErrorLogScroll.ScrollToEnd();
            }));
}
