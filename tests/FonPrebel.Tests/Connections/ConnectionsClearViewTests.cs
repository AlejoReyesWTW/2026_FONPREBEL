using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using FonPrebel.Presentation.ViewModels;
using Serilog.Core;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// §2.14: el botón "Limpiar vista" del panel Conexiones vacía sólo el mini-log de la pantalla. No borra,
/// no recorta y no abre los archivos de registro en disco, y los intentos nuevos vuelven a aparecer.
/// </summary>
public sealed class ConnectionsClearViewTests
{
    private const string FileServerKey = "file-server";
    private const string EmailKey = "email";

    [Fact]
    public async Task Clearing_the_view_empties_the_mini_log_of_both_cards()
    {
        RecordingConnectionMonitor monitor = new();
        using ConnectionsViewModel viewModel = new(monitor);

        await monitor.CheckNowAsync(FileServerKey);
        await monitor.CheckNowAsync(EmailKey);

        Assert.All(viewModel.Targets, target => Assert.NotEmpty(target.LogLines));

        viewModel.ClearViewCommand.Execute(null);

        Assert.All(viewModel.Targets, target => Assert.Empty(target.LogLines));
    }

    [Fact]
    public async Task A_new_attempt_appears_again_after_clearing_the_view()
    {
        RecordingConnectionMonitor monitor = new();
        using ConnectionsViewModel viewModel = new(monitor);

        await monitor.CheckNowAsync(FileServerKey);
        viewModel.ClearViewCommand.Execute(null);

        await monitor.CheckNowAsync(FileServerKey);

        ConnectionTargetViewModel card = Assert.Single(
            viewModel.Targets.Where(target => target.TargetKey == FileServerKey));

        string line = Assert.Single(card.LogLines);

        Assert.Contains("intento 2", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clearing_the_view_does_not_touch_the_log_files_on_disk()
    {
        RecordingConnectionMonitor monitor = new();
        using ConnectionsViewModel viewModel = new(monitor);
        using TempDirectory temp = new();

        AppPaths paths = new(temp.Path);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        {
            logger.Information("Comprobación de conexiones de prueba.");
        }

        string logFile = paths.ExecutionLogPath(DateOnly.FromDateTime(DateTime.Today));
        string before = await File.ReadAllTextAsync(logFile);
        Assert.NotEmpty(before);

        await monitor.CheckNowAsync(FileServerKey);
        viewModel.ClearViewCommand.Execute(null);

        Assert.True(File.Exists(logFile));
        Assert.Equal(before, await File.ReadAllTextAsync(logFile));
        Assert.Empty(viewModel.Targets.SelectMany(target => target.LogLines));
    }

    [Fact]
    public async Task Without_clearing_the_view_the_mini_log_keeps_every_attempt()
    {
        RecordingConnectionMonitor monitor = new();
        using ConnectionsViewModel viewModel = new(monitor);

        await monitor.CheckNowAsync(FileServerKey);
        await monitor.CheckNowAsync(FileServerKey);

        ConnectionTargetViewModel card = Assert.Single(
            viewModel.Targets.Where(target => target.TargetKey == FileServerKey));

        Assert.Equal(2, card.LogLines.Count);
        Assert.Contains("intento 1", card.LogLines[0], StringComparison.Ordinal);
        Assert.Contains("intento 2", card.LogLines[1], StringComparison.Ordinal);
    }
}

/// <summary>
/// Monitor de mentira con momentos crecientes: permite comprobar qué se ve después de limpiar la vista
/// sin depender de la hora real ni de la red.
/// </summary>
internal sealed class RecordingConnectionMonitor : IConnectionMonitor
{
    private static readonly DateTimeOffset Origin = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private ConnectionTargetSnapshot[] _snapshots =
    [
        new("file-server", "Servidor de archivos", null, false, []),
        new("email", "Correo", null, false, []),
    ];

    private int _attempts;

    public IReadOnlyList<ConnectionTargetSnapshot> Snapshots => _snapshots;

    public TimeSpan? Interval => TimeSpan.FromMinutes(2);

    public int? SlowThresholdMilliseconds => null;

    public DateTimeOffset? LastCycleAtUtc => null;

    public event EventHandler? Updated;

    public Task CheckNowAsync(string targetKey, CancellationToken cancellationToken = default)
    {
        int attempt = ++_attempts;
        DateTimeOffset moment = Origin.AddMinutes(attempt);

        _snapshots = _snapshots
            .Select(snapshot => snapshot.TargetKey == targetKey
                ? snapshot with
                {
                    LastResult = Result(snapshot, attempt, moment),
                    Entries = snapshot.Entries
                        .Append(Result(snapshot, attempt, moment))
                        .ToArray(),
                }
                : snapshot)
            .ToArray();

        Updated?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private static ConnectionProbeResult Result(
        ConnectionTargetSnapshot snapshot,
        int attempt,
        DateTimeOffset moment) =>
        ConnectionProbeResult.Online(
            snapshot.TargetKey,
            snapshot.TargetName,
            moment,
            durationMilliseconds: 8,
            message: $"Responde (intento {attempt}).");
}
