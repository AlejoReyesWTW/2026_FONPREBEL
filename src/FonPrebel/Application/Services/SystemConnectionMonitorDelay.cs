using FonPrebel.Application.Interfaces;

namespace FonPrebel.Application.Services;

/// <summary>
/// Espera real del rechequeo automático (§2.14). Es una clase sin estado: la aplicación la registra una
/// sola vez y las pruebas la sustituyen por una espera controlada.
/// </summary>
public sealed class SystemConnectionMonitorDelay : IConnectionMonitorDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
        Task.Delay(interval, cancellationToken);
}
