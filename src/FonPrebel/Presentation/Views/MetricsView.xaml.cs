using System.Windows;
using System.Windows.Controls;
using FonPrebel.Presentation.ViewModels;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Vista de la sección Métricas (ficha V5, §2.22). Sólo presenta las tarjetas que arma el ViewModel: no
/// calcula indicadores ni decide porcentajes por su cuenta.
/// </summary>
/// <remarks>
/// Al mostrarse la sección se pide una lectura nueva de la base; al cerrarse se cancela lo pendiente, así
/// cambiar de sección con una lectura en curso no deja trabajo de fondo ni toca una pantalla que ya no está.
/// </remarks>
public partial class MetricsView : UserControl
{
    public MetricsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MetricsViewModel viewModel)
        {
            viewModel.Activate();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MetricsViewModel viewModel)
        {
            viewModel.Deactivate();
        }
    }
}
