using System.Windows;
using System.Windows.Controls;
using FonPrebel.Presentation.Views;
using FonPrebel.Presentation.Views.Layout;
using WpfApplication = System.Windows.Application;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// S25: la distribución vive en un solo lugar, <c>PageLayout.xaml</c>, y las vistas que lo adoptaron lo usan
/// como raíz. Si una vista vuelve a armar su propio contenedor, esta prueba lo dice en lugar de dejarlo
/// pasar como una decisión de estilo.
/// </summary>
public sealed class PageLayoutAdoptionTests
{
    private const string LayoutUri = "/FonPrebel;component/Presentation/Views/Layout/PageLayout.xaml";

    [Fact]
    public void The_shared_layout_centres_forms_at_960_with_padding_32()
    {
        StaTest.Run(() =>
        {
            ResourceDictionary dictionary = Assert.IsType<ResourceDictionary>(
                WpfApplication.LoadComponent(new Uri(LayoutUri, UriKind.Relative)));

            Style style = Assert.IsType<Style>(dictionary[typeof(PageLayout)]);

            Setter width = Assert.Single(
                style.Setters.OfType<Setter>(),
                setter => setter.Property == PageLayout.ContentMaxWidthProperty);
            Setter padding = Assert.Single(
                style.Setters.OfType<Setter>(),
                setter => setter.Property == PageLayout.PagePaddingProperty);

            Assert.Equal(960d, Assert.IsType<double>(width.Value));
            Assert.Equal(new Thickness(32, 0, 32, 0), Assert.IsType<Thickness>(padding.Value));
        });
    }

    [Fact]
    public void Every_view_that_adopted_the_shared_layout_uses_it_as_its_root()
    {
        StaTest.Run(() =>
        {
            (string Name, Func<UserControl> Create)[] views =
            [
                ("Configuración", () => new SettingsView()),
                ("Conexiones", () => new ConnectionsView()),
                ("Historial", () => new HistoryView()),
                ("Logs", () => new LogsView()),
                ("Inicio de sesión", () => new LoginView()),
            ];

            foreach ((string name, Func<UserControl> create) in views)
            {
                UserControl view = create();

                Assert.True(
                    view.Content is PageLayout,
                    $"La vista '{name}' dejó de usar el contenedor compartido de S25.");
            }
        });
    }
}
