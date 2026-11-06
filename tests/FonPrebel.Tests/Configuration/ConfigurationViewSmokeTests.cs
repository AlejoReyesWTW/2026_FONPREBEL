using System.Windows;
using System.Windows.Controls;
using FonPrebel.Presentation.Views;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Comprobación de estructura, sin interacción: las dos vistas de esta entrega se cargan, reciben su
/// ViewModel y se disponen en pantalla sin errores de XAML. No se prueba la apariencia.
/// </summary>
public sealed class ConfigurationViewSmokeTests
{
    [Fact]
    public async Task Settings_view_loads_with_the_configuration_view_model()
    {
        using ConfigurationTestContext context = new();
        await context.LoadAsync();

        AssertViewLoads(() => new SettingsView { DataContext = context.Settings });
    }

    [Fact]
    public void Execution_view_loads_with_the_execution_view_model()
    {
        using ConfigurationTestContext context = new();

        AssertViewLoads(() => new ExecutionView { DataContext = context.Execution });
    }

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
