namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Claves registradas en la tabla <c>AppConfiguration</c> (§3.6 del diseño de datos). Son las únicas
/// que admiten lectura y escritura: un parámetro nuevo exige registrarlo acá para que no se inventen
/// claves desde la interfaz (S6).
/// </summary>
/// <remarks>
/// Sólo dos claves tienen un valor confirmado por el spec: el intervalo de verificación automática de
/// 2 minutos y el modo WAL activado (§2.9.1 y §3.6). El resto arranca con <c>Value</c> nulo, que
/// significa "pendiente de configurar", y la pantalla lo muestra vacío.
/// </remarks>
public static class AppConfigurationKeys
{
    /// <summary>Ruta 1: consolidado anual (§2.12).</summary>
    public const string Route1 = "FileServer.Route1";

    /// <summary>Ruta 2: los 16 archivos de póliza (§2.12).</summary>
    public const string Route2 = "FileServer.Route2";

    /// <summary>Ruta 3: archivo del analista, que la fase 2 sobrescribe (§2.12).</summary>
    public const string Route3 = "FileServer.Route3";

    /// <summary>Ruta 4: carpeta de PDFs (§2.12).</summary>
    public const string Route4 = "FileServer.Route4";

    /// <summary>Carpeta local de salida de informes (§2.6); ruta de datos, no valor de negocio.</summary>
    public const string OutputFolder = "Output.Folder";

    /// <summary>Intervalo de verificación automática en minutos; único valor confirmado: 2 (§2.9.1).</summary>
    public const string AutoCheckMinutes = "Connections.AutoCheckMinutes";

    /// <summary>Umbral de conexión lenta en milisegundos; pendiente (§2.14).</summary>
    public const string SlowThresholdMs = "Connections.SlowThresholdMs";

    /// <summary>Interruptor de proxy (§2.9.1).</summary>
    public const string ProxyEnabled = "Mail.ProxyEnabled";

    /// <summary>Texto del proxy; obligatorio sólo si el interruptor está encendido (§2.9.2 r7).</summary>
    public const string ProxyText = "Mail.ProxyText";

    /// <summary>Tiempo máximo de espera por envío, en segundos; pendiente (§2.9.1).</summary>
    public const string SendTimeoutSeconds = "Mail.SendTimeoutSeconds";

    /// <summary>Tiempo de espera ante bloqueo de SQLite, en milisegundos; pendiente (§5.4).</summary>
    public const string BusyTimeoutMs = "Database.BusyTimeoutMs";

    /// <summary>Modo WAL; el spec ordena activarlo, así que el valor confirmado es <c>true</c> (§3.6).</summary>
    public const string WalEnabled = "Database.WalEnabled";

    /// <summary>Trabajadores para PDFs; el paralelismo está pendiente (§2.15).</summary>
    public const string PdfWorkers = "Workers.PdfCount";

    /// <summary>Trabajadores para correos; el envío es secuencial, uno por uno (§2.15).</summary>
    public const string EmailWorkers = "Workers.EmailCount";

    /// <summary>Las claves registradas, en el orden en que se siembran.</summary>
    public static IReadOnlyList<string> Registered { get; } = new[]
    {
        Route1,
        Route2,
        Route3,
        Route4,
        OutputFolder,
        AutoCheckMinutes,
        SlowThresholdMs,
        ProxyEnabled,
        ProxyText,
        SendTimeoutSeconds,
        BusyTimeoutMs,
        WalEnabled,
        PdfWorkers,
        EmailWorkers,
    };

    /// <summary>Indica si la clave está registrada en el diseño de datos.</summary>
    public static bool IsRegistered(string key) => Registered.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// Valor inicial confirmado de una clave, o nulo cuando el spec no lo fija. "Ningún parámetro tiene
    /// un valor por defecto inventado" (§2.9.1).
    /// </summary>
    public static string? ConfirmedInitialValue(string key) => key switch
    {
        AutoCheckMinutes => "2",
        WalEnabled => "true",
        _ => null,
    };
}
