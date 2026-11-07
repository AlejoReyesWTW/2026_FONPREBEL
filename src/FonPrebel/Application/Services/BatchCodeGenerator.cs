using System.Globalization;
using FonPrebel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Application.Services;

/// <summary>
/// §2.8 reglas 1 a 3: identificador único y legible del lote. <b>El formato es provisional</b>: el ejemplo
/// <c>2026-11-002</c> del spec es ilustrativo y no un formato obligatorio, así que la primera vez que se
/// genera un identificador queda el aviso en el log para que nadie lo dé por definitivo.
/// </summary>
/// <remarks>
/// Este archivo es el único que conoce el formato: generar y leer la secuencia pasan por acá. Cuando el
/// funcional confirme el formato definitivo (§5.3), se cambia únicamente esta clase.
/// </remarks>
public sealed class BatchCodeGenerator : IBatchCodeGenerator
{
    /// <summary>Ejemplo ilustrativo del §2.8 regla 3; se usa como valor provisional.</summary>
    public const string ProvisionalExample = "2026-11-002";

    /// <summary>Descripción del patrón provisional: año, mes y secuencia del periodo.</summary>
    public const string ProvisionalPattern = "AAAA-MM-NNN";

    private const int ProvisionalSequenceWidth = 3;

    private readonly ILogger<BatchCodeGenerator> _logger;
    private int _pendingFormatNotice;

    public BatchCodeGenerator(ILogger<BatchCodeGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public string Generate(int periodYear, int periodMonth, int sequence)
    {
        if (periodYear is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodYear),
                "El año del periodo del lote debe tener cuatro dígitos.");
        }

        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodMonth),
                "El mes del periodo del lote debe estar entre 1 y 12.");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "La secuencia del lote dentro del periodo empieza en 1.");
        }

        WarnProvisionalFormatOnce();

        return string.Concat(
            periodYear.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            periodMonth.ToString("D2", CultureInfo.InvariantCulture),
            "-",
            sequence.ToString("D" + ProvisionalSequenceWidth.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public bool TryReadSequence(string code, int periodYear, int periodMonth, out int sequence)
    {
        sequence = 0;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string prefix = string.Concat(
            periodYear.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            periodMonth.ToString("D2", CultureInfo.InvariantCulture),
            "-");

        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string tail = code[prefix.Length..];
        if (tail.Length == 0 || !tail.All(char.IsAsciiDigit))
        {
            return false;
        }

        return int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }

    /// <summary>Aviso único: el formato no está confirmado y no debe darse por definitivo (§2.8 regla 3).</summary>
    private void WarnProvisionalFormatOnce()
    {
        if (Interlocked.Exchange(ref _pendingFormatNotice, 1) == 1)
        {
            return;
        }

        _logger.LogWarning(
            "El formato del identificador de lote es provisional y está pendiente de confirmación (§2.8 regla 3): " +
            "se usa {Patron}, por ejemplo {Ejemplo}, sólo como valor provisional. El ejemplo del spec no es un formato obligatorio.",
            ProvisionalPattern,
            ProvisionalExample);
    }
}
