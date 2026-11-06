using System.Windows;
using System.Windows.Threading;
using FonPrebel.Infrastructure.Database;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using FonPrebel.Infrastructure.Security;
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
/// <remarks>
/// El tipo base se califica porque <c>FonPrebel.Application</c> es la capa de contratos y, dentro del
/// espacio de nombres <c>FonPrebel</c>, oculta al <c>Application</c> de WPF.
/// </remarks>
public partial class App : System.Windows.Application
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
                services.AddFonPrebelDatabase(paths);

                // S7: la credencial de correo se guarda cifrada con DPAPI y se descifra bajo demanda.
                services.AddFonPrebelSecurity();

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

        // S3: la base se prepara antes de abrir la ventana; si no se pudo, el error se muestra de forma
        // visible y la aplicación se cierra, en vez de quedar abierta sin persistencia.
        DatabaseStartupService database = _host.Services.GetRequiredService<DatabaseStartupService>();
        if (!database.Succeeded)
        {
            ShowDatabaseStartupError(database.Failure);
            Shutdown(1);
            base.OnStartup(e);

            return;
        }

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

    /// <summary>
    /// S3: el detalle del fallo de la base se muestra en pantalla y queda en <c>errors.log</c>; no se
    /// borra ni se recrea el archivo, y el mensaje no incluye credenciales.
    /// </summary>
    private static void ShowDatabaseStartupError(Exception? failure)
    {
        string detail = failure?.Message ?? "Error desconocido";

        MessageBox.Show(
            "No se pudo preparar la base de datos local.\n\n" +
            $"Detalle: {detail}\n\n" +
            "La base existente no se borró ni se recreó. Revisa el archivo errors.log del día antes de " +
            "volver a abrir la aplicación.",
            "Base de datos no disponible",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
