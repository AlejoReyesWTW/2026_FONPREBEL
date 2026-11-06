using System.Windows;
using FonPrebel.Presentation.ViewModels;
using FonPrebel.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FonPrebel;

/// <summary>
/// Arranque de la aplicación: el contenedor de dependencias del host arma el grafo de objetos y la
/// ventana principal se resuelve de él. No hay estado global más allá del host.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
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
        await _host.StartAsync();

        MainWindow window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();

        base.OnExit(e);
    }
}
