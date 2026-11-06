namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Configuración: parámetros y credenciales. Habilita el botón INICIAR.</summary>
public sealed class SettingsViewModel : SectionViewModel
{
    public SettingsViewModel()
        : base("settings", "Configuración",
               "Correo, proxy, rutas del servidor de archivos, intervalos y credenciales. Mientras falte un campo obligatorio, el proceso no arranca.")
    {
    }
}
