using System.Windows.Controls;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Sección Configuración (V4): muestra las secciones colapsables con los campos validados y la gestión
/// de credenciales. Toda la lógica vive en el ViewModel; el code-behind sólo inicializa el XAML.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
