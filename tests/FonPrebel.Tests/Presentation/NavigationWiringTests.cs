using System.Collections;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FonPrebel.Presentation.Navigation;
using FonPrebel.Presentation.Views;
using Wpf.Ui.Controls;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// S16, cableado del clic: la barra lateral no navegaba porque el ítem se buscaba recorriendo el árbol
/// visual desde el origen del evento. Estas pruebas construyen los ítems reales y comprueban que cada uno
/// lleva su clave en <c>Tag</c>, que están las siete secciones en el orden del spec, que Inicio de sesión
/// va al pie y deshabilitado, y que activar un ítem navega a esa sección.
/// </summary>
public sealed class NavigationWiringTests
{
    private static readonly string[] ExpectedKeys =
    {
        "execution",
        "history",
        "logs",
        "settings",
        "metrics",
        "connections",
        "login",
    };

    [Fact]
    public void Items_keep_the_catalog_order_with_the_section_key_in_the_tag()
    {
        StaTest.Run(() =>
        {
            (ArrayList menu, ArrayList footer, _) = BuildItems();

            string[] menuKeys = Tags(menu);
            string[] footerKeys = Tags(footer);

            Assert.Equal(ExpectedKeys.Take(6).ToArray(), menuKeys);
            Assert.Equal(new[] { "login" }, footerKeys);
        });
    }

    [Fact]
    public void Every_item_shows_the_business_title_and_the_catalog_enabled_state()
    {
        StaTest.Run(() =>
        {
            (ArrayList menu, ArrayList footer, _) = BuildItems();

            NavigationViewItem[] items = menu.Cast<NavigationViewItem>().Concat(footer.Cast<NavigationViewItem>()).ToArray();

            Assert.Equal(7, items.Length);
            Assert.All(items, item => Assert.IsType<SymbolIcon>(item.Icon));

            foreach (SectionDefinition section in SectionCatalog.Sections)
            {
                NavigationViewItem item = items.Single(candidate => (string?)candidate.Tag == section.Key);

                Assert.Equal(section.Title, item.Content);
                Assert.Equal(section.IsEnabled, item.IsEnabled);
            }
        });
    }

    [Fact]
    public void The_footer_section_is_disabled_so_it_cannot_be_opened()
    {
        StaTest.Run(() =>
        {
            (_, ArrayList footer, _) = BuildItems();

            NavigationViewItem login = Assert.IsType<NavigationViewItem>(Assert.Single(footer));

            Assert.Equal("login", login.Tag);
            Assert.False(login.IsEnabled);
        });
    }

    [Fact]
    public void Clicking_a_section_item_navigates_to_that_section()
    {
        StaTest.Run(() =>
        {
            List<string> navigated = new();
            SectionNavigationBinder binder = new(navigated.Add);
            NavigationViewItem settings = binder.CreateItem(Section("settings"));

            settings.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            });

            Assert.Equal(new[] { "settings" }, navigated);
        });
    }

    [Fact]
    public void Clicking_a_disabled_item_does_not_navigate()
    {
        StaTest.Run(() =>
        {
            List<string> navigated = new();
            SectionNavigationBinder binder = new(navigated.Add);
            NavigationViewItem login = binder.CreateItem(Section("login"));

            login.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            });

            Assert.Empty(navigated);
        });
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void Activating_an_item_with_the_keyboard_navigates_the_same_way(Key key)
    {
        StaTest.Run(() =>
        {
            List<string> navigated = new();
            SectionNavigationBinder binder = new(navigated.Add);
            NavigationViewItem connections = binder.CreateItem(Section("connections"));

            using HwndSource source = new(new HwndSourceParameters("FonPrebel.Tests.Keyboard")
            {
                Width = 1,
                Height = 1,
            });

            KeyEventArgs args = new(Keyboard.PrimaryDevice, source, 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };

            connections.RaiseEvent(args);

            Assert.Equal(new[] { "connections" }, navigated);
        });
    }

    [Fact]
    public void An_unknown_key_never_reaches_the_navigation_and_leaves_the_section_untouched()
    {
        StaTest.Run(() =>
        {
            List<string> navigated = new();
            SectionNavigationBinder binder = new(navigated.Add);
            NavigationViewItem item = binder.CreateItem(Section("settings"));
            item.Tag = null;

            item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            });

            Assert.Empty(navigated);
        });
    }

    private static (ArrayList Menu, ArrayList Footer, List<string> Navigated) BuildItems()
    {
        List<string> navigated = new();
        SectionNavigationBinder binder = new(navigated.Add);
        ArrayList menu = new();
        ArrayList footer = new();

        SectionNavigationBinder.Build(SectionCatalog.Sections, binder, menu, footer);

        return (menu, footer, navigated);
    }

    private static string[] Tags(IEnumerable items) =>
        items.Cast<NavigationViewItem>().Select(item => (string?)item.Tag ?? string.Empty).ToArray();

    private static SectionDefinition Section(string key) =>
        SectionCatalog.Sections.Single(section => section.Key == key);
}
