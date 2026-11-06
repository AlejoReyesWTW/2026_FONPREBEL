using FonPrebel.Domain.Entities;
using FonPrebel.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// Contexto de la base local SQLite (S3). Se crea siempre desde <see cref="IDbContextFactory{TContext}"/>:
/// una instancia por operación, nunca compartida entre hilos.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Batch> Batches => Set<Batch>();

    public DbSet<BatchExecution> BatchExecutions => Set<BatchExecution>();

    public DbSet<PhaseExecution> PhaseExecutions => Set<PhaseExecution>();

    public DbSet<BatchRecipient> BatchRecipients => Set<BatchRecipient>();

    public DbSet<EmailConfiguration> EmailConfigurations => Set<EmailConfiguration>();

    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();

    public DbSet<ExecutionInputFingerprint> ExecutionInputFingerprints => Set<ExecutionInputFingerprint>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModelConventions.Apply(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
