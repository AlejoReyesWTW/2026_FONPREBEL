namespace FonPrebel.Domain.Entities;

/// <summary>
/// Configuración de correo y secreto cifrado (§2.9, tabla <c>EmailConfiguration</c> del §3.5). Es una
/// sola fila: la restricción <c>CHECK (Id = 1)</c> lo garantiza.
/// </summary>
/// <remarks>
/// Todos los valores reales están pendientes de confirmar (§2.9.1): las columnas existen y arrancan
/// nulas. No hay columnas de proxy ni de tiempo de espera porque §2.9.1 las ubica en
/// <see cref="AppConfiguration"/>, ni una columna de autenticación porque el mecanismo está pendiente
/// (§2.10).
/// </remarks>
public sealed class EmailConfiguration
{
    /// <summary>Clave primaria; la tabla sólo admite el valor 1.</summary>
    public int Id { get; set; }

    /// <summary>Servidor de correo.</summary>
    public string? Host { get; set; }

    /// <summary>Puerto del servidor de correo.</summary>
    public int? Port { get; set; }

    /// <summary>Seguridad de la conexión; los valores previstos son <c>None</c>, <c>StartTls</c> y <c>SslOnConnect</c>.</summary>
    public string? Security { get; set; }

    /// <summary>Cuenta de correo; queda nula si el mecanismo es autenticación integrada.</summary>
    public string? Account { get; set; }

    /// <summary>Remitente visible; se configura, nunca se escribe en el código.</summary>
    public string? SenderAddress { get; set; }

    /// <summary>Contraseña cifrada con DPAPI <c>CurrentUser</c>: el único dato cifrado de la base.</summary>
    public byte[]? PasswordCipher { get; set; }

    /// <summary>Momento del último cambio del secreto, en UTC.</summary>
    public DateTime? PasswordUpdatedAtUtc { get; set; }

    /// <summary>Momento de la última actualización del registro, en UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
