namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Conexiones: prueba del servidor de archivos y del servidor de correo.</summary>
public sealed class ConnectionsViewModel : SectionViewModel
{
    public ConnectionsViewModel()
        : base("connections", "Conexiones",
               "Dos pruebas independientes, servidor de archivos y correo, con su mini-log y su punto de estado. Se reverifica cada dos minutos en segundo plano.")
    {
    }
}
