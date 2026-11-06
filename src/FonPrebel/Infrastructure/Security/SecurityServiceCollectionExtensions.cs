using FonPrebel.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FonPrebel.Infrastructure.Security;

/// <summary>
/// Registro de las credenciales cifradas (S7): el proveedor DPAPI y el servicio de credencial de
/// correo. Ambos son sin estado entre operaciones —cada llamada abre su propio contexto de base—, así
/// que viven como singletons del host, igual que el resto de la infraestructura.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>Registra el proveedor de credenciales y el servicio de la credencial de correo.</summary>
    /// <remarks>
    /// El validador remoto (§2.10.1 paso 2) no se registra todavía: el protocolo de correo del banco
    /// está pendiente (§2.10) y la aplicación no llama a ningún servidor. Cuando exista, su registro
    /// entra acá antes de <see cref="IEmailCredentialService"/>.
    /// </remarks>
    public static IServiceCollection AddFonPrebelSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICredentialProvider, DpapiCredentialProvider>();
        services.AddSingleton<IEmailCredentialService, EmailCredentialService>();

        return services;
    }
}
