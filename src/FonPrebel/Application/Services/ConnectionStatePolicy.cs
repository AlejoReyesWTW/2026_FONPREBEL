using FonPrebel.Application.Interfaces;

namespace FonPrebel.Application.Services;

/// <summary>
/// Decisión de "conexión lenta" (§2.14). El umbral en milisegundos está pendiente en la especificación
/// (§2.14, §5.6), así que sin un valor configurado y positivo nunca se marca amarillo: el resultado se
/// queda en verde o en rojo, que es lo único que la comprobación puede afirmar.
/// </summary>
public static class ConnectionStatePolicy
{
    /// <summary>
    /// Aplica el umbral vigente a un resultado que respondió. Un umbral nulo, cero o negativo deja el
    /// resultado como está; un umbral configurado convierte en <see cref="ConnectionProbeStatus.Slow"/>
    /// la respuesta que lo supere.
    /// </summary>
    /// <param name="result">Resultado de la sonda.</param>
    /// <param name="slowThresholdMilliseconds">Umbral vigente, o nulo si sigue pendiente.</param>
    public static ConnectionProbeResult ApplySlowThreshold(
        ConnectionProbeResult result,
        int? slowThresholdMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status != ConnectionProbeStatus.Online)
        {
            return result;
        }

        if (result.DurationMilliseconds is not long elapsedMilliseconds)
        {
            return result;
        }

        if (NormalizeThreshold(slowThresholdMilliseconds) is not int threshold)
        {
            return result;
        }

        if (elapsedMilliseconds <= threshold)
        {
            return result;
        }

        return result with
        {
            Status = ConnectionProbeStatus.Slow,
            Message =
                $"Responde, pero lento: {elapsedMilliseconds} ms superan el umbral de {threshold} ms definido en Configuración.",
        };
    }

    /// <summary>Umbral utilizable, o nulo cuando el parámetro está pendiente o no es positivo.</summary>
    public static int? NormalizeThreshold(int? slowThresholdMilliseconds) =>
        slowThresholdMilliseconds is int value && value > 0 ? value : null;
}
