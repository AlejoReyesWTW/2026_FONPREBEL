namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Parámetros de base de datos que se leen de <c>AppConfiguration</c> una vez al iniciar y que el
/// interceptor aplica a cada conexión abierta (S3, §3.6 y §6 del diseño de datos).
/// </summary>
public interface IDatabaseRuntimeSettings
{
    /// <summary>Indica si se activa el modo WAL; <c>true</c> mientras el parámetro esté pendiente.</summary>
    bool WalEnabled { get; }

    /// <summary>Tiempo de espera ante bloqueo en milisegundos; nulo significa "pendiente".</summary>
    int? BusyTimeoutMs { get; }
}
