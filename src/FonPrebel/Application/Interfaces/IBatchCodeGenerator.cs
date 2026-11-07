namespace FonPrebel.Application.Interfaces;

/// <summary>
/// Genera el identificador único y legible de un lote (<c>Batch.Code</c>, §2.8 reglas 1 a 3) y sabe
/// leerlo de vuelta, para que el formato viva en un solo lugar.
/// </summary>
/// <remarks>
/// <para>
/// El formato definitivo está <b>pendiente de confirmación</b> (§2.8 regla 3, §5.3): <c>2026-11-002</c> es
/// sólo el ejemplo ilustrativo del spec y <b>no</b> un formato obligatorio. La implementación actual usa
/// ese ejemplo como valor provisional y lo avisa en el log la primera vez que se usa. Cuando el funcional
/// confirme el formato, se cambia únicamente la implementación de este contrato.
/// </para>
/// <para>
/// El identificador no se inventa por lote: el número de secuencia lo aporta quien crea el lote, que es el
/// único que conoce los lotes ya guardados del periodo.
/// </para>
/// </remarks>
public interface IBatchCodeGenerator
{
    /// <summary>Genera el identificador de un lote para un periodo y un número de secuencia del periodo.</summary>
    /// <param name="periodYear">Año del periodo del lote.</param>
    /// <param name="periodMonth">Mes del periodo, de 1 a 12.</param>
    /// <param name="sequence">Número de secuencia dentro del periodo: 1 para el primer lote.</param>
    string Generate(int periodYear, int periodMonth, int sequence);

    /// <summary>
    /// Lee el número de secuencia de un identificador del periodo indicado. Devuelve falso cuando el
    /// identificador no pertenece a ese periodo, de modo que quien crea el lote no tenga que conocer el
    /// formato.
    /// </summary>
    bool TryReadSequence(string code, int periodYear, int periodMonth, out int sequence);
}
