namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Espera entre ciclos del rechequeo automático (§2.14). Es un contrato aparte para que el monitor no
/// dependa de <see cref="Task.Delay(TimeSpan, CancellationToken)"/>: las pruebas controlan el reloj sin
/// esperar minutos reales, y la aplicación usa <c>SystemConnectionMonitorDelay</c>.
/// </summary>
public interface IConnectionMonitorDelay
{
    /// <summary>Espera el intervalo indicado; se cancela al detener el host.</summary>
    Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken);
}
