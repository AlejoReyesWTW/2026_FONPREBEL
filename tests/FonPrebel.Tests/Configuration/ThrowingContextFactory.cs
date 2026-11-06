using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// Factoría de contextos que falla siempre: representa una base local inaccesible y permite comprobar
/// que los almacenes y la pantalla devuelven un resultado tipado en vez de una excepción.
/// </summary>
internal sealed class ThrowingContextFactory : IDbContextFactory<AppDbContext>
{
    public const string FailureMessage = "La base local no está disponible (prueba).";

    public AppDbContext CreateDbContext() => throw new InvalidOperationException(FailureMessage);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(FailureMessage);
}
