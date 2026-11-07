using System.Windows.Controls;

namespace FonPrebel.Presentation.Views.Shared;

/// <summary>
/// Barras de progreso de la pantalla Ejecución (§2.18): una por fase, con su porcentaje y su contador. Sólo
/// presenta los ítems que arma el ViewModel; no calcula porcentajes por su cuenta.
/// </summary>
public partial class PhaseProgressList : UserControl
{
    public PhaseProgressList()
    {
        InitializeComponent();
    }
}
