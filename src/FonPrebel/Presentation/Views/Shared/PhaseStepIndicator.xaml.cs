using System.Windows.Controls;

namespace FonPrebel.Presentation.Views.Shared;

/// <summary>
/// Indicador de las cinco fases de la pantalla Ejecución (§2.18): un punto por fase con su número, su
/// nombre y su estado. Sólo presenta los ítems que arma el ViewModel; no calcula estados ni porcentajes.
/// </summary>
public partial class PhaseStepIndicator : UserControl
{
    public PhaseStepIndicator()
    {
        InitializeComponent();
    }
}
