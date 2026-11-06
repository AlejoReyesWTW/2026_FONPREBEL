using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FonPrebel.Infrastructure.Database;

/// <summary>
/// S3: cada conexión que EF abre contra <c>FonPrebel.db</c> recibe el modo WAL y, cuando el parámetro
/// <c>Database.BusyTimeoutMs</c> está configurado, el tiempo de espera ante bloqueo.
/// </summary>
/// <remarks>
/// Microsoft.Data.Sqlite no expone una API para <c>journal_mode</c> ni para <c>busy_timeout</c>: las
/// sentencias <c>PRAGMA</c> de abajo son configuración del motor y no consultas de negocio. Si el
/// tiempo de espera está pendiente no se envía ningún <c>PRAGMA</c>, de modo que el motor conserva su
/// valor por omisión.
/// </remarks>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private readonly IDatabaseRuntimeSettings _settings;

    public SqlitePragmaInterceptor(IDatabaseRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);

        return Task.CompletedTask;
    }

    private void ApplyPragmas(DbConnection connection)
    {
        if (connection is not SqliteConnection)
        {
            return;
        }

        if (_settings.WalEnabled)
        {
            Execute(connection, "PRAGMA journal_mode = WAL;");
        }

        if (_settings.BusyTimeoutMs is int timeoutMs)
        {
            Execute(connection, string.Create(CultureInfo.InvariantCulture, $"PRAGMA busy_timeout = {timeoutMs};"));
        }
    }

    private static void Execute(DbConnection connection, string pragma)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = pragma;
        command.ExecuteNonQuery();
    }
}
