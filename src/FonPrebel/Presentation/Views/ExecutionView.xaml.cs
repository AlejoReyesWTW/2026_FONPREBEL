using System.Windows;
using System.Windows.Controls;
using FonPrebel.Presentation.ViewModels;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Sección Ejecución (ficha V1, §2.18). Sólo presenta lo que arma el ViewModel: el lote activo, el
/// indicador de fases, las barras por fase y las acciones con su gate. No calcula progreso ni estados.
/// </summary>
/// <remarks>
/// Al mostrarse la sección se lee el estado persistido del lote; al cerrarse se cancela la lectura pendiente,
/// así cambiar de sección con una consulta en curso no deja trabajo de fondo sobre una pantalla que ya no está.
/// </remarks>
public partial class ExecutionView : UserControl
{
    public ExecutionView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExecutionViewModel viewModel)
        {
            viewModel.Activate();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExecutionViewModel viewModel)
        {
            viewModel.Deactivate();
        }
    }
}
