using FonPrebel.Application.Interfaces;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Monitor de conexiones de prueba: registra qué destinos se comprobaron y permite fijar el resultado de
/// cada comprobación, incluido el caso en que la sonda no se pudo hacer y hay que informar el motivo.
/// </summary>
internal sealed class FakeConnectionMonitor : IConnectionMonitor
{
    private ConnectionTargetSnapshot[] _snapshots =
    [
        new("file-server", "Servidor de archivos", null, false, []),
        new("email", "Correo", null, false, []),
    ];

    /// <summary>Resultado que devuelve cada comprobación; por defecto responde en verde.</summary>
    public Func<string, ConnectionProbeResult> ResultFactory { get; set; } = key => ConnectionProbeResult.Online(
        key,
        key == "email" ? "Correo" : "Servidor de archivos",
        DateTimeOffset.UnixEpoch,
        durationMilliseconds: 12,
        message: "Respondió en 12 ms.");

    /// <summary>Claves comprobadas, en orden, para probar que la pantalla no comprueba de más.</summary>
    public List<string> CheckedKeys { get; } = [];

    public IReadOnlyList<ConnectionTargetSnapshot> Snapshots => _snapshots;

    public TimeSpan? Interval => TimeSpan.FromMinutes(2);

    public int? SlowThresholdMilliseconds => null;

    public DateTimeOffset? LastCycleAtUtc => null;

    public event EventHandler? Updated
    {
        add { }
        remove { }
    }

    public Task CheckNowAsync(string targetKey, CancellationToken cancellationToken = default)
    {
        CheckedKeys.Add(targetKey);

        ConnectionProbeResult result = ResultFactory(targetKey);

        _snapshots = _snapshots
            .Select(snapshot => snapshot.TargetKey == targetKey
                ? snapshot with
                {
                    LastResult = result,
                    Entries = snapshot.Entries.Append(result).ToArray(),
                }
                : snapshot)
            .ToArray();

        return Task.CompletedTask;
    }
}
