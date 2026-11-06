namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Historial: consulta de lotes anteriores, sin tocarlos.</summary>
public sealed class HistoryViewModel : SectionViewModel
{
    public HistoryViewModel()
        : base("history", "Historial",
               "Consulta de lotes anteriores, sus ejecuciones, el estado de cada fase y el resultado de cada notificación. Sólo lectura.")
    {
    }
}
