using System.Globalization;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Presentation.ViewModels.Execution;

/// <summary>
/// Rótulos visibles de la pantalla Ejecución (ficha V1, §2.18): nombres de fase, etiquetas legibles de los
/// estados persistidos y colores de estado. Es el único lugar que traduce un estado del diseño a texto en
/// español: la vista no compone rótulos por su cuenta.
/// </summary>
/// <remarks>
/// Los nombres de fase son los del §3 del spec (fase 1 a fase 5). Los estados de <see cref="PhaseStatus"/> y
/// <see cref="BatchStatus"/> conservan su nombre en el dominio porque son literales del diseño; acá se
/// muestran con una etiqueta legible y se conserva el literal como texto alternativo en la vista.
/// </remarks>
internal static class ExecutionLabels
{
    /// <summary>Etiqueta de una fase cuando todavía no hay ninguna ejecución de la que leer su estado.</summary>
    internal const string NoBatchStatus = "Sin lote";

    /// <summary>Color de una fase pendiente: el punto va vacío, con el borde gris.</summary>
    internal const string PendingColor = "#8A8A8A";

    /// <summary>Color de una fase que está trabajando.</summary>
    internal const string RunningColor = "#0F6CBD";

    /// <summary>Color de una fase que espera la confirmación del analista.</summary>
    internal const string WaitingColor = "#9D5D00";

    /// <summary>Color de una fase completada.</summary>
    internal const string CompletedColor = "#107C10";

    /// <summary>Color de una fase fallida.</summary>
    internal const string FailedColor = "#C42B1C";

    /// <summary>Color de una fase cancelada.</summary>
    internal const string CancelledColor = "#6B6B6B";

    private static readonly string[] PhaseNames =
    {
        "Consolidación",
        "Cruce y completado",
        "Generación de PDFs",
        "Envío por correo",
        "Informe de notificaciones",
    };

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Nombre de la fase, en el orden del §3 del spec.</summary>
    internal static string PhaseName(int phase) =>
        phase >= 1 && phase <= PhaseNames.Length ? PhaseNames[phase - 1] : $"Fase {phase}";

    /// <summary>Etiqueta legible del estado de una fase.</summary>
    internal static string PhaseStatusLabel(PhaseStatus status) => status switch
    {
        PhaseStatus.PENDIENTE => "Pendiente",
        PhaseStatus.EN_EJECUCION => "En ejecución",
        PhaseStatus.ESPERANDO_CONFIRMACION => "Esperando confirmación",
        PhaseStatus.COMPLETADA => "Completada",
        PhaseStatus.FALLIDA => "Fallida",
        PhaseStatus.CANCELADA => "Cancelada",
        _ => status.ToString(),
    };

    /// <summary>Etiqueta legible del estado del lote y de la ejecución (Anexo F.3).</summary>
    internal static string BatchStatusLabel(BatchStatus status) => status switch
    {
        BatchStatus.LISTO => "Listo",
        BatchStatus.EJECUTANDO => "En ejecución",
        BatchStatus.PAUSADO_POR_INCONSISTENCIA => "Pausado por inconsistencia",
        BatchStatus.VALIDANDO_RECUPERACION => "Validando recuperación",
        BatchStatus.LISTO_PARA_REANUDAR => "Listo para reanudar",
        BatchStatus.COMPLETADO => "Completado",
        BatchStatus.CANCELADO => "Cancelado",
        _ => status.ToString(),
    };

    /// <summary>Color del estado de una fase: el mismo que usa su punto.</summary>
    internal static string PhaseStatusColor(PhaseStatus status) => status switch
    {
        PhaseStatus.EN_EJECUCION => RunningColor,
        PhaseStatus.ESPERANDO_CONFIRMACION => WaitingColor,
        PhaseStatus.COMPLETADA => CompletedColor,
        PhaseStatus.FALLIDA => FailedColor,
        PhaseStatus.CANCELADA => CancelledColor,
        _ => PendingColor,
    };

    /// <summary>Color del estado del lote. Un lote en curso y uno terminado se leen distinto.</summary>
    internal static string BatchStatusColor(BatchStatus status) => status switch
    {
        BatchStatus.EJECUTANDO => RunningColor,
        BatchStatus.VALIDANDO_RECUPERACION => RunningColor,
        BatchStatus.PAUSADO_POR_INCONSISTENCIA => WaitingColor,
        BatchStatus.LISTO_PARA_REANUDAR => WaitingColor,
        BatchStatus.COMPLETADO => CompletedColor,
        BatchStatus.CANCELADO => CancelledColor,
        _ => RunningColor,
    };

    /// <summary>Periodo del lote como "Noviembre de 2026".</summary>
    internal static string Period(int year, int month)
    {
        if (month is < 1 or > 12)
        {
            return $"{month:D2}/{year}";
        }

        string name = Spanish.DateTimeFormat.GetMonthName(month);

        return $"{char.ToUpper(name[0], Spanish)}{name[1..]} de {year}";
    }

    /// <summary>Fecha y hora local legible: la base guarda UTC y el analista lee su hora.</summary>
    internal static string Date(DateTime utcMoment) =>
        utcMoment.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Spanish);

    /// <summary>Hora local de la última lectura, para el pie de la pantalla.</summary>
    internal static string Time(DateTime moment) => moment.ToString("HH:mm:ss", Spanish);

    /// <summary>Cifra con separador de miles, para "1.234 de 4.500".</summary>
    internal static string Count(int value) => value.ToString("N0", Spanish);

    /// <summary>Porcentaje redondeado sin decimales, para "42 %".</summary>
    internal static string Percentage(double value) => Math.Round(value).ToString("0", Spanish) + " %";
}
