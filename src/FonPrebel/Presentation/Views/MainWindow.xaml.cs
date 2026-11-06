using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using FonPrebel.Presentation.Navigation;
using FonPrebel.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Ventana principal. La barra lateral se construye desde <see cref="SectionCatalog"/>, de modo que
/// el orden y los títulos vivan en un solo lugar, y la sección visible se resuelve en el ViewModel.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        BuildNavigationItems();

        Navigation.ItemInvoked += OnNavigationItemInvoked;

        if (Navigation.MenuItems.Count > 0 && Navigation.MenuItems[0] is NavigationViewItem first)
        {
            first.IsActive = true;
        }
    }

    private void BuildNavigationItems()
    {
        foreach (SectionDefinition section in SectionCatalog.Sections)
        {
            NavigationViewItem item = new()
            {
                Content = section.Title,
                Icon = new SymbolIcon(section.Icon),
                Tag = section.Key,
                IsEnabled = section.IsEnabled,
            };

            if (SectionCatalog.FooterSectionKeys.Contains(section.Key))
            {
                Navigation.FooterMenuItems.Add(item);
            }
            else
            {
                Navigation.MenuItems.Add(item);
            }
        }
    }

    private void OnNavigationItemInvoked(object sender, RoutedEventArgs e)
    {
        NavigationViewItem? invoked = FindNavigationViewItem(e.OriginalSource as DependencyObject)
            ?? Navigation.SelectedItem as NavigationViewItem;

        if (invoked?.Tag is string sectionKey)
        {
            _viewModel.Navigate(sectionKey);
        }
    }

    private static NavigationViewItem? FindNavigationViewItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is NavigationViewItem item)
            {
                return item;
            }

            if (source is NavigationView)
            {
                return null;
            }

            source = source is Visual or Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }
}
