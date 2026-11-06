using System.Windows.Controls;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Vista de la sección Conexiones (V6, §2.14). Sólo presenta el estado que publica el ViewModel: no
/// comprueba conexiones ni decide colores por su cuenta.
/// </summary>
public partial class ConnectionsView : UserControl
{
    public ConnectionsView()
    {
        InitializeComponent();
    }
}
