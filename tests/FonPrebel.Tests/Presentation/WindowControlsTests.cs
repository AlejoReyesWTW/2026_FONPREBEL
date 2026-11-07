using System.Windows;
using FonPrebel.Presentation.Views;
using Wpf.Ui.Appearance;

namespace FonPrebel.Tests.Presentation;

/// <summary>
/// S16 (§2.20) y S24: el tema claro/oscuro alterna de verdad, el modo compacto achica la ventana, la deja
/// por encima del resto y devuelve tamaño y posición anteriores al desactivarlo, y la ventana nunca queda
/// fuera del monitor actual, que es el caso de quien trabaja con dos pantallas a escalas distintas.
/// </summary>
public sealed class WindowControlsTests
{
    private static readonly Rect PrimaryWorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void The_theme_toggle_switches_between_light_and_dark()
    {
        Assert.Equal(ApplicationTheme.Dark, MainWindow.NextTheme(ApplicationTheme.Light));
        Assert.Equal(ApplicationTheme.Light, MainWindow.NextTheme(ApplicationTheme.Dark));
    }

    [Fact]
    public void An_unknown_theme_starts_with_the_dark_theme()
    {
        Assert.Equal(ApplicationTheme.Dark, MainWindow.NextTheme(ApplicationTheme.Unknown));
        Assert.Equal(ApplicationTheme.Dark, MainWindow.NextTheme(ApplicationTheme.HighContrast));
    }

    [Fact]
    public void The_window_controls_describe_themselves_in_spanish()
    {
        Assert.Equal("Cambiar a tema oscuro", MainWindow.ThemeDarkToolTip);
        Assert.Equal("Cambiar a tema claro", MainWindow.ThemeLightToolTip);
        Assert.Equal("Modo compacto siempre en primer plano", MainWindow.CompactModeOnToolTip);
        Assert.Equal("Salir del modo compacto", MainWindow.CompactModeOffToolTip);
    }

    [Fact]
    public void The_compact_mode_shrinks_the_window_and_keeps_it_on_top()
    {
        StaTest.Run(() =>
        {
            Window window = new() { Width = 1180, Height = 760, Left = 120, Top = 90, MinWidth = 980, MinHeight = 620 };

            CompactWindowState state = CompactWindowState.Capture(window, PrimaryWorkArea);
            state.Apply(window, PrimaryWorkArea);

            Assert.Equal(CompactWindowState.CompactWidth, window.Width);
            Assert.Equal(CompactWindowState.CompactHeight, window.Height);
            Assert.Equal(CompactWindowState.CompactWidth, window.MinWidth);
            Assert.Equal(CompactWindowState.CompactHeight, window.MinHeight);
            Assert.True(window.Topmost);
            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

    [Fact]
    public void Leaving_the_compact_mode_restores_the_previous_size_position_and_topmost()
    {
        StaTest.Run(() =>
        {
            Window window = new() { Width = 1180, Height = 760, Left = 120, Top = 90, MinWidth = 980, MinHeight = 620 };

            CompactWindowState state = CompactWindowState.Capture(window, PrimaryWorkArea);
            state.Apply(window, PrimaryWorkArea);
            state.Restore(window, PrimaryWorkArea);

            Assert.Equal(1180, window.Width);
            Assert.Equal(760, window.Height);
            Assert.Equal(120, window.Left);
            Assert.Equal(90, window.Top);
            Assert.Equal(980, window.MinWidth);
            Assert.Equal(620, window.MinHeight);
            Assert.False(window.Topmost);
            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

    [Fact]
    public void A_maximized_window_returns_to_maximized_after_the_compact_mode()
    {
        StaTest.Run(() =>
        {
            Window window = new() { Width = 1180, Height = 760, Left = 120, Top = 90 };
            window.WindowState = WindowState.Maximized;

            CompactWindowState state = CompactWindowState.Capture(window, PrimaryWorkArea);
            state.Apply(window, PrimaryWorkArea);

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.Topmost);

            state.Restore(window, PrimaryWorkArea);

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.False(window.Topmost);
        });
    }

    [Fact]
    public void Placement_keeps_the_window_inside_the_work_area()
    {
        Rect placed = CompactWindowState.Placement(PrimaryWorkArea, 1180, 760, 4000, 4000);

        Assert.Equal(1920 - 1180, placed.Left);
        Assert.Equal(1040 - 760, placed.Top);
        Assert.Equal(1180, placed.Width);
        Assert.Equal(760, placed.Height);
    }

    [Fact]
    public void Placement_keeps_the_same_offset_when_it_fits()
    {
        Rect placed = CompactWindowState.Placement(PrimaryWorkArea, 1180, 760, 120, 90);

        Assert.Equal(120, placed.Left);
        Assert.Equal(90, placed.Top);
    }

    [Fact]
    public void The_restored_size_fits_a_smaller_second_monitor()
    {
        StaTest.Run(() =>
        {
            // La ventana estaba grande en el monitor principal y el modo compacto se movió al segundo.
            Window window = new() { Width = 1180, Height = 760, Left = 120, Top = 90 };
            Rect secondMonitor = new(1920, 0, 1366, 728);

            CompactWindowState state = CompactWindowState.Capture(window, PrimaryWorkArea);
            state.Apply(window, secondMonitor);

            Assert.True(window.Width <= secondMonitor.Width, "El modo compacto debe caber en el monitor actual.");
            Assert.True(window.Left >= secondMonitor.Left, "La ventana no puede quedar fuera del monitor actual.");

            state.Restore(window, secondMonitor);

            Assert.True(window.Width <= secondMonitor.Width, "Al restaurar, la ventana debe caber en el monitor actual.");
            Assert.True(window.Height <= secondMonitor.Height, "Al restaurar, la ventana debe caber en el monitor actual.");
            Assert.True(window.Left >= secondMonitor.Left, "Al restaurar, la ventana debe quedar dentro del monitor actual.");
            Assert.True(window.Top >= secondMonitor.Top, "Al restaurar, la ventana debe quedar dentro del monitor actual.");
        });
    }
}
