namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Logs: lectura de las dos bitácoras diarias dentro de la aplicación.</summary>
public sealed class LogsViewModel : SectionViewModel
{
    public LogsViewModel()
        : base("logs", "Logs",
               "Dos paneles, execution.log y errors.log, con fecha, hora, nivel, lote, ejecución, fase y resultado. Los logs van a archivo, nunca a la base.")
    {
    }
}
