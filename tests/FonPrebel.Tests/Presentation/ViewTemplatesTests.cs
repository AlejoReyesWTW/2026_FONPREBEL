using System.Windows;
using FonPrebel.Presentation.Navigation;
using WpfApplication = System.Windows.Application;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.Views;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// S16 y corrección del cableado de la UI: lo que nunca se verificó es que la sección activa se convierta
/// en su vista. Estas pruebas resuelven, una por una, las plantillas que la aplicación usa para mostrar
/// cada ViewModel de sección y comprueban que la plantilla produce realmente la vista esperada.
/// </summary>
public sealed class ViewTemplatesTests
{
    private const string TemplatesUri = "/FonPrebel;component/Presentation/Resources/ViewTemplates.xaml";

    private static readonly Dictionary<Type, Type> ExpectedViewsByViewModel = new()
    {
        [typeof(ExecutionViewModel)] = typeof(ExecutionView),
        [typeof(HistoryViewModel)] = typeof(HistoryView),
        [typeof(LogsViewModel)] = typeof(LogsView),
        [typeof(SettingsViewModel)] = typeof(SettingsView),
        [typeof(MetricsViewModel)] = typeof(MetricsView),
        [typeof(ConnectionsViewModel)] = typeof(ConnectionsView),
        [typeof(LoginViewModel)] = typeof(LoginView),
    };

    [Fact]
    public void The_view_templates_dictionary_is_packaged_with_the_application()
    {
        StaTest.Run(() =>
        {
            Assert.NotNull(WpfApplication.GetResourceStream(new Uri(TemplatesUri, UriKind.Relative)));
        });
    }

    [Fact]
    public void Every_section_view_model_has_a_template_that_builds_its_view()
    {
        StaTest.Run(() =>
        {
            ResourceDictionary templates = LoadTemplates();

            foreach (KeyValuePair<Type, Type> pair in ExpectedViewsByViewModel)
            {
                DataTemplate? template = templates[new DataTemplateKey(pair.Key)] as DataTemplate;

                Assert.True(template is not null, $"Falta la plantilla de '{pair.Key.Name}'.");

                object content = template!.LoadContent();

                Assert.IsType(pair.Value, content);
            }
        });
    }

    [Fact]
    public void The_templates_cover_exactly_the_seven_sections_of_the_catalog()
    {
        StaTest.Run(() =>
        {
            ResourceDictionary templates = LoadTemplates();

            string[] withoutTemplate = SectionCatalog.Sections
                .Where(section => templates[new DataTemplateKey(ViewModelTypeOf(section.Key))] is not DataTemplate)
                .Select(section => section.Key)
                .ToArray();

            Assert.Empty(withoutTemplate);
            Assert.Equal(7, ExpectedViewsByViewModel.Count);
        });
    }

    /// <summary>Tipos de ViewModel que la ventana resuelve por clave de sección, en el orden del spec.</summary>
    private static readonly Dictionary<string, Type> ViewModelTypesBySectionKey = new(StringComparer.Ordinal)
    {
        ["execution"] = typeof(ExecutionViewModel),
        ["history"] = typeof(HistoryViewModel),
        ["logs"] = typeof(LogsViewModel),
        ["settings"] = typeof(SettingsViewModel),
        ["metrics"] = typeof(MetricsViewModel),
        ["connections"] = typeof(ConnectionsViewModel),
        ["login"] = typeof(LoginViewModel),
    };

    private static Type ViewModelTypeOf(string sectionKey) => ViewModelTypesBySectionKey[sectionKey];

    private static ResourceDictionary LoadTemplates()
    {
        object? loaded = WpfApplication.LoadComponent(new Uri(TemplatesUri, UriKind.Relative));

        return Assert.IsType<ResourceDictionary>(loaded);
    }
}
