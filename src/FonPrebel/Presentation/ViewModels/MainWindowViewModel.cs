using CommunityToolkit.Mvvm.ComponentModel;
using FonPrebel.Presentation.Navigation;

namespace FonPrebel.Presentation.ViewModels;

/// <summary>ViewModel de la ventana principal: mantiene la sección activa y resuelve la navegación.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IReadOnlyDictionary<string, SectionViewModel> _sections;

    public MainWindowViewModel(
        ExecutionViewModel execution,
        HistoryViewModel history,
        LogsViewModel logs,
        SettingsViewModel settings,
        MetricsViewModel metrics,
        ConnectionsViewModel connections,
        LoginViewModel login)
    {
        _sections = new Dictionary<string, SectionViewModel>(StringComparer.Ordinal)
        {
            [execution.SectionKey] = execution,
            [history.SectionKey] = history,
            [logs.SectionKey] = logs,
            [settings.SectionKey] = settings,
            [metrics.SectionKey] = metrics,
            [connections.SectionKey] = connections,
            [login.SectionKey] = login,
        };

        CurrentSection = _sections[SectionCatalog.DefaultSectionKey];
    }

    /// <summary>Secciones en el orden obligatorio de la barra lateral.</summary>
    public IReadOnlyList<SectionDefinition> Sections => SectionCatalog.Sections;

    /// <summary>ViewModel de la sección visible.</summary>
    [ObservableProperty]
    private SectionViewModel _currentSection = null!;

    /// <summary>Cambia la sección visible. Una clave desconocida no cambia nada.</summary>
    public void Navigate(string sectionKey)
    {
        if (_sections.TryGetValue(sectionKey, out SectionViewModel? section))
        {
            CurrentSection = section;
        }
    }
}
