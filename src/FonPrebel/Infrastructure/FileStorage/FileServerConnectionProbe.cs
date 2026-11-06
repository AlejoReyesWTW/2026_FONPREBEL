using System.Diagnostics;
using System.IO;
using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Validation;

namespace FonPrebel.Infrastructure.FileStorage;

/// <summary>
/// §2.14: comprobación del servidor de archivos. Lee las cuatro rutas guardadas en SQLite y verifica que
/// las que están configuradas existan y sean alcanzables y legibles.
/// </summary>
/// <remarks>
/// <para>
/// La sonda no afirma nada que no haya comprobado: sin ninguna ruta configurada el estado es
/// <see cref="ConnectionProbeStatus.Unknown"/> con el motivo "sin configurar", nunca verde ni rojo.
/// Sólo escribe en el mini-log el resultado de una verificación real sobre el sistema de archivos.
/// </para>
/// <para>
/// La comprobación es de lectura: abre los archivos para leer y enumera las carpetas, sin crear ni
/// modificar nada. La validación de escritura de la pantalla Configuración es otra comprobación y
/// pertenece a <see cref="FileRouteValidation"/>.
/// </para>
/// </remarks>
public sealed class FileServerConnectionProbe : IConnectionProbe
{
    /// <summary>Clave estable del destino.</summary>
    public const string Key = "file-server";

    /// <summary>Nombre visible del destino.</summary>
    public const string DisplayName = "Servidor de archivos";

    /// <summary>Motivo cuando no hay ninguna ruta configurada.</summary>
    public const string NotConfiguredReason = "sin configurar";

    /// <summary>Motivo cuando la configuración guardada no se pudo leer.</summary>
    public const string ConfigurationUnavailableReason = "no se pudo leer la configuración";

    private static readonly RouteTarget[] RouteTargets =
    {
        new(AppConfigurationKeys.Route1, FileRouteValidation.Route1Parameter, true),
        new(AppConfigurationKeys.Route2, FileRouteValidation.Route2Parameter, false),
        new(AppConfigurationKeys.Route3, FileRouteValidation.Route3Parameter, true),
        new(AppConfigurationKeys.Route4, FileRouteValidation.Route4Parameter, false),
    };

    private readonly IAppConfigurationStore _configurationStore;

    public FileServerConnectionProbe(IAppConfigurationStore configurationStore)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);

        _configurationStore = configurationStore;
    }

    /// <inheritdoc />
    public string TargetKey => Key;

    /// <inheritdoc />
    public string TargetName => DisplayName;

    /// <inheritdoc />
    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset attemptedAt = DateTimeOffset.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<(RouteTarget Target, string Path)> configuredRoutes = new();

        foreach (RouteTarget target in RouteTargets)
        {
            ConfigurationReadResult<string> read = await _configurationStore
                .ReadTextAsync(target.Key, cancellationToken)
                .ConfigureAwait(false);

            if (!read.Succeeded)
            {
                return ConnectionProbeResult.Unchecked(
                    TargetKey,
                    TargetName,
                    attemptedAt,
                    ConfigurationUnavailableReason,
                    $"Sin comprobar: no se pudo leer la configuración ({read.Outcome}).");
            }

            if (!string.IsNullOrWhiteSpace(read.Value))
            {
                configuredRoutes.Add((target, read.Value.Trim()));
            }
        }

        if (configuredRoutes.Count == 0)
        {
            return ConnectionProbeResult.Unchecked(
                TargetKey,
                TargetName,
                attemptedAt,
                NotConfiguredReason,
                "Sin comprobar: sin configurar. Configura las rutas del servidor de archivos en Configuración.");
        }

        List<string> failures = new();

        foreach ((RouteTarget target, string path) in configuredRoutes)
        {
            string? failure = target.ExpectsFile ? CheckFile(path) : CheckFolder(path);

            if (failure is not null)
            {
                failures.Add($"{target.ParameterName}: {failure}");
            }
        }

        stopwatch.Stop();
        long durationMilliseconds = (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);

        if (failures.Count > 0)
        {
            string reason = string.Join(" ", failures);

            return ConnectionProbeResult.Offline(
                TargetKey,
                TargetName,
                attemptedAt,
                durationMilliseconds,
                reason,
                $"No responde: {reason}");
        }

        return ConnectionProbeResult.Online(
            TargetKey,
            TargetName,
            attemptedAt,
            durationMilliseconds,
            $"Responde: {configuredRoutes.Count} de {RouteTargets.Length} rutas configuradas existen y se pueden leer.");
    }

    /// <summary>Abre el archivo para lectura: es el permiso real que la aplicación necesita en la ruta.</summary>
    private static string? CheckFile(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return stream.CanRead ? null : "no se puede leer.";
        }
        catch (FileNotFoundException)
        {
            return "el archivo no existe.";
        }
        catch (DirectoryNotFoundException)
        {
            return "la ruta no existe o el servidor no responde.";
        }
        catch (UnauthorizedAccessException)
        {
            return "no se puede leer: revisa los permisos.";
        }
        catch (IOException exception)
        {
            return $"el servidor no responde ({exception.Message}).";
        }
        catch (NotSupportedException)
        {
            return "la ruta no tiene un formato válido.";
        }
    }

    /// <summary>Enumera la carpeta, que es la comprobación real de que está alcanzable y legible.</summary>
    private static string? CheckFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return "la carpeta no existe o el servidor no responde.";
        }

        try
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            _ = entries.MoveNext();

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return "no se puede leer: revisa los permisos.";
        }
        catch (IOException exception)
        {
            return $"el servidor no responde ({exception.Message}).";
        }
        catch (NotSupportedException)
        {
            return "la ruta no tiene un formato válido.";
        }
    }

    /// <summary>Una de las cuatro rutas con su clave, nombre visible y tipo esperado.</summary>
    private sealed record RouteTarget(string Key, string ParameterName, bool ExpectsFile);
}
