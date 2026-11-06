namespace FonPrebel.Presentation.ViewModels;

/// <summary>Sección Métricas: indicadores acumulados calculados desde la base local.</summary>
public sealed class MetricsViewModel : SectionViewModel
{
    public MetricsViewModel()
        : base("metrics", "Métricas",
               "Indicadores filtrables por periodo y por lote, con numerador y denominador explícitos. Los indicadores definitivos están pendientes de definir.")
    {
    }
}
