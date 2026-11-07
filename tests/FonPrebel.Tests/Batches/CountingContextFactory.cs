using FonPrebel.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// Factoría de contextos que delega en la de la base temporal y anota cada contexto que entrega: así se
/// comprueba que ninguna operación comparte el contexto con otra (S3, §1 y §6 del diseño de datos).
/// </summary>
internal sealed class CountingContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly IDbContextFactory<AppDbContext> _inner;
    private readonly List<AppDbContext> _created = new();
    private readonly object _sync = new();

    public CountingContextFactory(IDbContextFactory<AppDbContext> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _created.Count;
            }
        }
    }

    public IReadOnlyList<AppDbContext> Created
    {
        get
        {
            lock (_sync)
            {
                return _created.ToArray();
            }
        }
    }

    public AppDbContext CreateDbContext() => Track(_inner.CreateDbContext());

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Track(await _inner.CreateDbContextAsync(cancellationToken).ConfigureAwait(false));

    private AppDbContext Track(AppDbContext context)
    {
        lock (_sync)
        {
            _created.Add(context);
        }

        return context;
    }
}
