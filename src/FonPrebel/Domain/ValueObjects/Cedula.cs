namespace FonPrebel.Domain.ValueObjects;

/// <summary>
/// Cédula del asociado tal como se compara y se guarda en <c>BatchRecipient.Cedula</c> (§3.4 del diseño de
/// datos): normalizada sin puntos, para que <c>8.123.456</c> y <c>8123456</c> sean el mismo jubilado.
/// </summary>
/// <remarks>
/// El formato de la cédula como contraseña del PDF está <b>pendiente</b> (§2.11), así que acá sólo se
/// resuelve la comparación: no se agregan ni se quitan dígitos verificadores ni se rellena con ceros.
/// </remarks>
public static class Cedula
{
    /// <summary>Normaliza la cédula quitando puntos y espacios; un valor vacío queda como cadena vacía.</summary>
    public static string Normalize(string? cedula)
    {
        if (string.IsNullOrWhiteSpace(cedula))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[cedula.Length];
        int length = 0;

        foreach (char character in cedula)
        {
            if (character is '.' or ' ' or '\u00A0' or '\t')
            {
                continue;
            }

            buffer[length] = character;
            length++;
        }

        return new string(buffer[..length]);
    }
}
