using System.Windows;
using System.Windows.Threading;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FonPrebel;

/// <summary>
/// Arranque de la aplicación: el contenedor de dependencias del host arma el grafo de objetos y la
/// ventana principal se resuelve de él. No hay estado global más allá del host y del logger.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // S5: la raíz de datos locales se resuelve una vez y se comparte con las bitácoras y las
        // secciones. El constructor sin parámetros usa %LOCALAPPDATA%\FonPrebel.
        AppPaths paths = new();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddFonPrebelLogging(paths);

                services.AddSingleton<ExecutionViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MetricsViewModel>();
                services.AddSingleton<ConnectionsViewModel>();
                services.AddSingleton<LoginViewModel>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        await _host.StartAsync();

        Log.Information("Aplicación iniciada");

        MainWindow window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Aplicación cerrada");

        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();

        // Cierra los archivos de bitácora del día en curso antes de salir.
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>
    /// S17: las excepciones no controladas se registran en <c>errors.log</c>; nunca se registran
    /// credenciales ni datos sensibles.
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Error no controlado en la interfaz");
    }
}
