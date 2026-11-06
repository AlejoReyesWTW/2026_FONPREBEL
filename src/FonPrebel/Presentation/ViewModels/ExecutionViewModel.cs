namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Ejecución: operar el lote y seguir el avance de las cinco fases.</summary>
public sealed class ExecutionViewModel : SectionViewModel
{
    public ExecutionViewModel()
        : base("execution", "Ejecución",
               "Acá el analista inicia, reanuda y observa el proceso: indicador de fases, barra global y una barra por fase.")
    {
    }
}
