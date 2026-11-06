using FonPrebel.Application.Interfaces;
using FonPrebel.Infrastructure.FileStorage;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// §2.14: la sonda del servidor de archivos sólo afirma lo que comprobó. Sin rutas configuradas no hay
/// verde ni rojo; con una ruta que responde hay verde; con una que no existe hay rojo con el motivo.
/// </summary>
public sealed class FileServerConnectionProbeTests
{
    [Fact]
    public async Task Without_configured_routes_the_state_is_unknown_and_never_green_or_red()
    {
        using ConnectionProbeTestContext context = new();
        FileServerConnectionProbe probe = context.CreateFileProbe();

        ConnectionProbeResult result = await probe.ProbeAsync();

        Assert.Equal(FileServerConnectionProbe.DisplayName, result.TargetName);
        Assert.Equal(ConnectionProbeStatus.Unknown, result.Status);
        Assert.Equal("sin configurar", result.Reason);
        Assert.Contains("sin configurar", result.Message);
        Assert.Null(result.DurationMilliseconds);
        Assert.NotEqual(ConnectionProbeStatus.Online, result.Status);
        Assert.NotEqual(ConnectionProbeStatus.Offline, result.Status);
        Assert.False(result.Responded);
    }

    [Fact]
    public async Task An_existing_and_readable_route_responds()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetRouteAsync(AppConfigurationKeys.Route1, context.CreateFile("consolidado-anual.xlsx"));

        ConnectionProbeResult result = await context.CreateFileProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Online, result.Status);
        Assert.True(result.Responded);
        Assert.NotNull(result.DurationMilliseconds);
        Assert.Null(result.Reason);
        Assert.Contains("1 de 4", result.Message);
    }

    [Fact]
    public async Task All_four_configured_routes_that_respond_leave_the_state_green()
    {
        using ConnectionProbeTestContext context = new();

        await context.SetRouteAsync(AppConfigurationKeys.Route1, context.CreateFile("consolidado-anual.xlsx"));
        await context.SetRouteAsync(AppConfigurationKeys.Route2, context.CreateFolder("polizas"));
        await context.SetRouteAsync(AppConfigurationKeys.Route3, context.CreateFile("analista.xlsx"));
        await context.SetRouteAsync(AppConfigurationKeys.Route4, context.CreateFolder("pdfs"));

        ConnectionProbeResult result = await context.CreateFileProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Online, result.Status);
        Assert.Contains("4 de 4", result.Message);
    }

    [Fact]
    public async Task A_missing_file_route_does_not_respond_and_reports_the_reason()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetRouteAsync(AppConfigurationKeys.Route1, context.MissingPath("no-existe.xlsx"));

        ConnectionProbeResult result = await context.CreateFileProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Offline, result.Status);
        Assert.False(result.Responded);
        Assert.NotNull(result.Reason);
        Assert.Contains("no existe", result.Reason);
        Assert.Contains("Ruta 1", result.Message);
        Assert.NotNull(result.DurationMilliseconds);
    }

    [Fact]
    public async Task A_missing_folder_route_does_not_respond_and_reports_the_reason()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetRouteAsync(AppConfigurationKeys.Route4, context.MissingPath("pdfs-que-no-existe"));

        ConnectionProbeResult result = await context.CreateFileProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Offline, result.Status);
        Assert.Contains("Ruta 4", result.Reason!);
        Assert.Contains("no existe", result.Reason);
    }

    [Fact]
    public async Task One_missing_route_makes_the_whole_check_red_and_names_every_failure()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetRouteAsync(AppConfigurationKeys.Route2, context.CreateFolder("polizas"));
        await context.SetRouteAsync(AppConfigurationKeys.Route3, context.MissingPath("analista.xlsx"));

        ConnectionProbeResult result = await context.CreateFileProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Offline, result.Status);
        Assert.Contains("Ruta 3", result.Reason!);
        Assert.DoesNotContain("Ruta 2", result.Reason);
    }
}
