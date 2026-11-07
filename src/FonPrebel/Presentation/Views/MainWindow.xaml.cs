using System.Collections;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FonPrebel.Presentation.Navigation;
using FonPrebel.Presentation.ViewModels;
using Serilog;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FonPrebel.Presentation.Views;

/// <summary>
/// Ventana principal. La barra lateral se construye desde <see cref="SectionCatalog"/>, de modo que
/// el orden y los títulos vivan en un solo lugar, y la sección visible se resuelve en el ViewModel.
/// </summary>
/// <remarks>
/// La barra de título propia (<c>ui:TitleBar</c>) es la superficie de arrastre y aporta minimizar,
/// maximizar/restaurar y cerrar. Los controles propios son el modo compacto (S24) y el tema (§2.20).
/// </remarks>
public partial class MainWindow : FluentWindow
{
    /// <summary>Texto del botón de tema cuando la aplicación está en claro.</summary>
    internal const string ThemeDarkToolTip = "Cambiar a tema oscuro";

    /// <summary>Texto del botón de tema cuando la aplicación está en oscuro.</summary>
    internal const string ThemeLightToolTip = "Cambiar a tema claro";

    /// <summary>Texto del botón de modo compacto cuando está apagado.</summary>
    internal const string CompactModeOnToolTip = "Modo compacto siempre en primer plano";

    /// <summary>Texto del botón de modo compacto cuando está encendido.</summary>
    internal const string CompactModeOffToolTip = "Salir del modo compacto";

    private readonly MainWindowViewModel _viewModel;
    private readonly SectionNavigationBinder _navigationBinder;

    private CompactWindowState? _compactState;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        _navigationBinder = new SectionNavigationBinder(NavigateToSection);

        InitializeComponent();

        DataContext = viewModel;

        SectionNavigationBinder.Build(
            SectionCatalog.Sections,
            _navigationBinder,
            Navigation.MenuItems,
            Navigation.FooterMenuItems);

        // La sección inicial, el contenido y el resaltado salen del mismo lugar: el catálogo.
        NavigateToSection(SectionCatalog.DefaultSectionKey);

        UpdateThemeToggle();
        UpdateCompactModeToggle();
    }

    /// <summary>Icono siguiente al actual. Sólo hay dos temas reales.</summary>
    internal static ApplicationTheme NextTheme(ApplicationTheme current) =>
        current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;

    /// <summary>
    /// Al cambiar de monitor la escala puede cambiar. Si el modo compacto está activo se vuelve a
    /// colocar dentro del monitor actual para no quedar con un tamaño absurdo ni fuera de la pantalla.
    /// </summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        _compactState?.Apply(this);
    }

    private void NavigateToSection(string sectionKey)
    {
        _viewModel.Navigate(sectionKey);
        MarkActiveSection(sectionKey);

        Log.Information("Sección activa: {SectionKey}", sectionKey);
    }

    /// <summary>El resaltado sigue a la sección visible; el setter de <c>SelectedItem</c> no es accesible.</summary>
    private void MarkActiveSection(string sectionKey)
    {
        foreach (IList items in new[] { Navigation.MenuItems, Navigation.FooterMenuItems })
        {
            foreach (object candidate in items)
            {
                if (candidate is NavigationViewItem item)
                {
                    item.IsActive = string.Equals(item.Tag as string, sectionKey, StringComparison.Ordinal);
                }
            }
        }
    }

    private void OnThemeToggleClicked(object sender, RoutedEventArgs e)
    {
        ApplicationTheme next = NextTheme(ApplicationThemeManager.GetAppTheme());

        // Sin efecto de fondo: el tema debe verse en toda la aplicación, no cambiar el marco de la ventana.
        ApplicationThemeManager.Apply(next, WindowBackdropType.None, updateAccent: true);

        UpdateThemeToggle();

        Log.Information("Tema de la aplicación: {Theme}", next);
    }

    private void UpdateThemeToggle()
    {
        bool isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

        ThemeModeIcon.Symbol = isDark ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24;

        string caption = isDark ? ThemeLightToolTip : ThemeDarkToolTip;
        ThemeToggleButton.ToolTip = caption;
        AutomationProperties.SetName(ThemeToggleButton, caption);
    }

    private void OnCompactModeToggled(object sender, RoutedEventArgs e)
    {
        if (_compactState is null)
        {
            _compactState = CompactWindowState.Capture(this);
            _compactState.Apply(this);
        }
        else
        {
            _compactState.Restore(this);
            _compactState = null;
        }

        UpdateCompactModeToggle();

        Log.Information("Modo compacto: {CompactMode}", _compactState is null ? "desactivado" : "activado");
    }

    private void UpdateCompactModeToggle()
    {
        bool isCompact = _compactState is not null;

        CompactModeIcon.Symbol = isCompact ? SymbolRegular.PinOff24 : SymbolRegular.Pin24;

        string caption = isCompact ? CompactModeOffToolTip : CompactModeOnToolTip;
        CompactModeButton.ToolTip = caption;
        AutomationProperties.SetName(CompactModeButton, caption);
    }
}

/// <summary>
/// Cablea cada <see cref="NavigationViewItem"/> de la barra lateral con la clave de su sección. Hay un solo
/// camino de navegación: el clic y el teclado llaman a <see cref="Activate"/>, que lee la clave del
/// <c>Tag</c> del propio ítem.
/// </summary>
/// <remarks>
/// No se recorre el árbol visual ni se usa <c>SelectedItem</c>: en WPF-UI 4.3.0 <c>ItemInvoked</c> no
/// entrega el ítem y el setter de <c>SelectedItem</c> no es accesible, así que cada ítem se cablea a mano.
/// </remarks>
internal sealed class SectionNavigationBinder
{
    private readonly Action<string> _navigate;

    internal SectionNavigationBinder(Action<string> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);

        _navigate = navigate;
    }

    /// <summary>Construye las siete secciones en el orden del catálogo, con Inicio de sesión al pie.</summary>
    internal static void Build(
        IEnumerable<SectionDefinition> sections,
        SectionNavigationBinder binder,
        IList menuItems,
        IList footerItems)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(menuItems);
        ArgumentNullException.ThrowIfNull(footerItems);

        foreach (SectionDefinition section in sections)
        {
            bool isFooter = SectionCatalog.FooterSectionKeys.Contains(section.Key, StringComparer.Ordinal);
            IList target = isFooter ? footerItems : menuItems;

            target.Add(binder.CreateItem(section));
        }
    }

    /// <summary>Un ítem con su título, icono, clave en <c>Tag</c> y sus dos formas de activación.</summary>
    internal NavigationViewItem CreateItem(SectionDefinition section)
    {
        ArgumentNullException.ThrowIfNull(section);

        NavigationViewItem item = new()
        {
            Content = section.Title,
            Icon = new SymbolIcon(section.Icon),
            Tag = section.Key,
            IsEnabled = section.IsEnabled,
        };

        item.AddHandler(
            UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnSectionClicked),
            handledEventsToo: true);

        item.PreviewKeyDown += OnSectionKeyDown;

        return item;
    }

    private void OnSectionClicked(object sender, MouseButtonEventArgs e) => Activate(sender);

    private void OnSectionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Activate(sender);
            e.Handled = true;
        }
    }

    /// <summary>Único punto que navega: una sección deshabilitada o sin clave no hace nada.</summary>
    private void Activate(object sender)
    {
        if (sender is NavigationViewItem { IsEnabled: true, Tag: string sectionKey } && sectionKey.Length > 0)
        {
            _navigate(sectionKey);
        }
    }
}

/// <summary>
/// S24: recuerda la ventana, aplica el modo compacto siempre en primer plano y al salir devuelve tamaño y
/// estado anteriores dentro del monitor actual, sin tocar el contenido ni la sección.
/// </summary>
internal sealed class CompactWindowState
{
    /// <summary>Ancho del modo compacto: deja lugar a la barra lateral y a la sección.</summary>
    internal const double CompactWidth = 640;

    /// <summary>Alto del modo compacto.</summary>
    internal const double CompactHeight = 640;

    private readonly double _width;
    private readonly double _height;
    private readonly double _offsetLeft;
    private readonly double _offsetTop;
    private readonly double _minWidth;
    private readonly double _minHeight;
    private readonly WindowState _windowState;
    private readonly bool _topmost;

    private CompactWindowState(Window window, Rect workArea)
    {
        _width = window.Width;
        _height = window.Height;
        _offsetLeft = window.Left - workArea.Left;
        _offsetTop = window.Top - workArea.Top;
        _minWidth = window.MinWidth;
        _minHeight = window.MinHeight;
        _windowState = window.WindowState;
        _topmost = window.Topmost;
    }

    internal static CompactWindowState Capture(Window window, Rect? workArea = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        return new CompactWindowState(window, workArea ?? WorkAreaOf(window));
    }

    /// <summary>Coloca un tamaño dentro del monitor actual sin dejarlo fuera de la pantalla.</summary>
    internal static Rect Placement(Rect workArea, double width, double height, double offsetLeft, double offsetTop)
    {
        double safeWidth = Math.Min(Math.Max(width, 0), workArea.Width);
        double safeHeight = Math.Min(Math.Max(height, 0), workArea.Height);

        double left = Math.Min(
            Math.Max(workArea.Left + offsetLeft, workArea.Left),
            Math.Max(workArea.Right - safeWidth, workArea.Left));
        double top = Math.Min(
            Math.Max(workArea.Top + offsetTop, workArea.Top),
            Math.Max(workArea.Bottom - safeHeight, workArea.Top));

        return new Rect(left, top, safeWidth, safeHeight);
    }

    internal void Apply(Window window, Rect? workArea = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        Rect monitor = workArea ?? WorkAreaOf(window);

        if (window.WindowState != WindowState.Normal)
        {
            window.WindowState = WindowState.Normal;
        }

        Rect target = Placement(
            monitor,
            CompactWidth,
            CompactHeight,
            window.Left - monitor.Left,
            window.Top - monitor.Top);

        // El mínimo se achica primero: si no, el tamaño compacto quedaría recortado al mínimo normal.
        window.MinWidth = CompactWidth;
        window.MinHeight = CompactHeight;
        window.Width = target.Width;
        window.Height = target.Height;
        window.Left = target.Left;
        window.Top = target.Top;
        window.Topmost = true;
    }

    internal void Restore(Window window, Rect? workArea = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Mismo desplazamiento dentro de su monitor, aplicado al monitor donde está ahora.
        Rect target = Placement(workArea ?? WorkAreaOf(window), _width, _height, _offsetLeft, _offsetTop);

        window.Topmost = _topmost;
        window.MinWidth = _minWidth;
        window.MinHeight = _minHeight;
        window.Width = target.Width;
        window.Height = target.Height;
        window.Left = target.Left;
        window.Top = target.Top;
        window.WindowState = _windowState;
    }

    /// <summary>Área útil del monitor actual en unidades independientes de la escala.</summary>
    internal static Rect WorkAreaOf(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            IntPtr monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };

            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(window);

                return new Rect(
                    info.Work.Left / dpi.DpiScaleX,
                    info.Work.Top / dpi.DpiScaleY,
                    (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
                    (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
            }
        }

        return SystemParameters.WorkArea;
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
