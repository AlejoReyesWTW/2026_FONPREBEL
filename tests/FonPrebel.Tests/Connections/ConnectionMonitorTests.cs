using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// §2.14: el monitor corre las dos sondas al iniciar y cada intervalo configurado, clasifica "lenta" sólo
/// con un umbral definido, acota y ordena el mini-log y convierte cualquier fallo en un resultado rojo
/// sin dejar caer el bucle.
/// </summary>
public sealed class ConnectionMonitorTests
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task The_startup_cycle_checks_both_targets_and_never_greens_without_configuration()
    {
        using ConnectionProbeTestContext context = new();
        using StepConnectionMonitorDelay delay = new();
        using ConnectionMonitor monitor = CreateMonitor(
            new IConnectionProbe[] { context.CreateFileProbe(), context.CreateEmailProbe() },
            context,
            delay);

        await monitor.StartAsync(CancellationToken.None);
        await StepConnectionMonitorDelay.WaitUntilAsync(
            () => monitor.Snapshots.All(snapshot => snapshot.LastResult is not null),
            WaitBudget);

        Assert.Equal(2, monitor.Snapshots.Count);
        Assert.All(monitor.Snapshots, snapshot => Assert.Equal(ConnectionProbeStatus.Unknown, snapshot.Status));
        Assert.All(monitor.Snapshots, snapshot => Assert.False(snapshot.LastResult!.Responded));
        Assert.NotNull(monitor.LastCycleAtUtc);

        // Sin valor guardado, el intervalo es el único que la especificación confirma: 2 minutos.
        Assert.Equal(TimeSpan.FromMinutes(2), monitor.Interval);

        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_interval_is_read_from_the_configuration_on_every_cycle()
    {
        using ConnectionProbeTestContext context = new();
        using StepConnectionMonitorDelay delay = new();
        FakeConnectionProbe files = new("file-server", "Servidor de archivos");
        FakeConnectionProbe mail = new("email", "Correo");
        using ConnectionMonitor monitor = CreateMonitor(new IConnectionProbe[] { files, mail }, context, delay);

        await context.SetAutoCheckMinutesAsync(2);

        await monitor.StartAsync(CancellationToken.None);
        await StepConnectionMonitorDelay.WaitUntilAsync(() => delay.Requested.Count >= 1, WaitBudget);

        Assert.Equal(TimeSpan.FromMinutes(2), delay.Requested[0]);
        Assert.Equal(1, files.Calls);

        await context.SetAutoCheckMinutesAsync(7);
        delay.Release();
        await StepConnectionMonitorDelay.WaitUntilAsync(() => delay.Requested.Count >= 2, WaitBudget);

        Assert.Equal(TimeSpan.FromMinutes(7), delay.Requested[1]);
        Assert.Equal(TimeSpan.FromMinutes(7), monitor.Interval);
        Assert.Equal(2, files.Calls);

        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Without_a_slow_threshold_a_long_response_is_never_reported_as_slow()
    {
        using ConnectionProbeTestContext context = new();
        using ConnectionMonitor monitor = CreateMonitor(
            new IConnectionProbe[] { FakeConnectionProbe.Responding("file-server", "Servidor de archivos", 30000) },
            context);

        await monitor.CheckNowAsync("file-server");

        ConnectionProbeResult result = monitor.Snapshots[0].LastResult!;

        Assert.Equal(ConnectionProbeStatus.Online, result.Status);
        Assert.Equal(30000, result.DurationMilliseconds);
        Assert.NotEqual(ConnectionProbeStatus.Slow, result.Status);
        Assert.Null(monitor.SlowThresholdMilliseconds);
    }

    [Fact]
    public async Task With_a_configured_threshold_a_long_response_is_reported_as_slow()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetSlowThresholdAsync(1000);
        using ConnectionMonitor monitor = CreateMonitor(
            new IConnectionProbe[] { FakeConnectionProbe.Responding("file-server", "Servidor de archivos", 30000) },
            context);

        await monitor.CheckNowAsync("file-server");

        ConnectionProbeResult result = monitor.Snapshots[0].LastResult!;

        Assert.Equal(ConnectionProbeStatus.Slow, result.Status);
        Assert.True(result.Responded);
        Assert.Equal(1000, monitor.SlowThresholdMilliseconds);
        Assert.Contains("umbral de 1000 ms", result.Message);
    }

    [Fact]
    public async Task A_failing_probe_becomes_a_red_result_with_its_reason_and_the_monitor_keeps_checking()
    {
        using ConnectionProbeTestContext context = new();
        using StepConnectionMonitorDelay delay = new();
        FakeConnectionProbe healthy = new("file-server", "Servidor de archivos");
        FakeConnectionProbe broken = new(
            "email",
            "Correo",
            _ => throw new InvalidOperationException("la sonda de prueba se rompió"));

        using ConnectionMonitor monitor = CreateMonitor(new IConnectionProbe[] { healthy, broken }, context, delay);

        await monitor.StartAsync(CancellationToken.None);
        await StepConnectionMonitorDelay.WaitUntilAsync(() => delay.Requested.Count >= 1, WaitBudget);

        ConnectionTargetSnapshot failing = monitor.Snapshots.Single(snapshot => snapshot.TargetKey == "email");
        Assert.Equal(ConnectionProbeStatus.Offline, failing.Status);
        Assert.Contains("la sonda de prueba se rompió", failing.LastResult!.Reason!);
        Assert.Equal(ConnectionProbeStatus.Online, monitor.Snapshots.Single(s => s.TargetKey == "file-server").Status);

        delay.Release();
        await StepConnectionMonitorDelay.WaitUntilAsync(() => broken.Calls >= 2, WaitBudget);

        Assert.Equal(2, broken.Calls);
        Assert.Equal(2, monitor.Snapshots.Single(snapshot => snapshot.TargetKey == "email").Entries.Count);

        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_mini_log_keeps_the_last_entries_in_order()
    {
        using ConnectionProbeTestContext context = new();
        using ConnectionMonitor monitor = CreateMonitor(
            new IConnectionProbe[] { new FakeConnectionProbe("file-server", "Servidor de archivos") },
            context);

        for (int attempt = 0; attempt < ConnectionMonitor.MaxLogEntries + 5; attempt++)
        {
            await monitor.CheckNowAsync("file-server");
        }

        IReadOnlyList<ConnectionProbeResult> entries = monitor.Snapshots[0].Entries;

        Assert.Equal(ConnectionMonitor.MaxLogEntries, entries.Count);
        Assert.Contains("intento 6", entries[0].Message);
        Assert.Contains("intento 35", entries[^1].Message);

        for (int index = 1; index < entries.Count; index++)
        {
            Assert.True(
                entries[index - 1].AttemptedAtUtc <= entries[index].AttemptedAtUtc,
                "El mini-log debe quedar en orden cronológico.");
        }
    }

    private static ConnectionMonitor CreateMonitor(
        IEnumerable<IConnectionProbe> probes,
        ConnectionProbeTestContext context,
        IConnectionMonitorDelay? delay = null) =>
        new(
            probes,
            context.ConfigurationStore,
            delay ?? new StepConnectionMonitorDelay(),
            NullLogger<ConnectionMonitor>.Instance);
}
