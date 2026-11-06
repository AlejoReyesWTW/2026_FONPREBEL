using System.Windows;
using System.Windows.Controls;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// Comprobación de estructura, sin interacción: la vista de Conexiones se carga con su ViewModel y se
/// dispone en pantalla sin errores de XAML. No se prueba la apariencia.
/// </summary>
public sealed class ConnectionsViewSmokeTests
{
    [Fact]
    public async Task Connections_view_loads_with_the_connections_view_model()
    {
        using ConnectionProbeTestContext context = new();
        using StepConnectionMonitorDelay delay = new();
        using ConnectionMonitor monitor = new(
            new IConnectionProbe[] { context.CreateFileProbe(), context.CreateEmailProbe() },
            context.ConfigurationStore,
            delay,
            NullLogger<ConnectionMonitor>.Instance);

        using ConnectionsViewModel viewModel = new(monitor);

        await monitor.CheckNowAsync(FileServerConnectionProbeKey);

        Assert.Equal(2, viewModel.Targets.Count);
        Assert.Contains("Rechequeo automático", viewModel.AutoCheckText);
        Assert.Contains("sin umbral definido", viewModel.SlowThresholdText);

        AssertViewLoads(() => new ConnectionsView { DataContext = viewModel });
    }

    private const string FileServerConnectionProbeKey = "file-server";

    /// <summary>Crea la vista en un hilo STA, la mide y la dispone, como hace WPF al mostrar la sección.</summary>
    private static void AssertViewLoads(Func<UserControl> create)
    {
        Exception? failure = null;

        Thread thread = new(() =>
        {
            try
            {
                UserControl view = create();

                view.Measure(new Size(1280, 900));
                view.Arrange(new Rect(0, 0, 1280, 900));
                view.UpdateLayout();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "El hilo de interfaz no terminó a tiempo.");
        Assert.Null(failure);
    }
}
