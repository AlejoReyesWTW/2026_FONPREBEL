using System.IO;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;
using FonPrebel.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FonPrebel.Tests;

/// <summary>
/// S3 y S4: la base se crea desde cero con las migraciones, conserva los datos entre aplicaciones,
/// queda en modo WAL y hace cumplir los índices y la restricción de fila única del diseño de datos.
/// </summary>
public sealed class DatabaseSchemaTests
{
    private static readonly string[] ExpectedTables =
    {
        "AppConfiguration",
        "Batch",
        "BatchExecution",
        "BatchRecipient",
        "EmailConfiguration",
        "ExecutionInputFingerprint",
        "PhaseExecution",
    };

    private static readonly DateTime SampleUtc = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Migrate_creates_the_seven_tables()
    {
        using TestDatabase database = new();

        database.Migrate();

        Assert.Equal(ExpectedTables, ReadTableNames(database.DatabasePath).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Migrating_twice_keeps_the_existing_rows()
    {
        using TestDatabase database = new();
        database.Migrate();

        using (AppDbContext context = database.CreateContext())
        {
            context.Batches.Add(NewBatch("2026-11-002", BatchStatus.LISTO));
            context.SaveChanges();
        }

        database.Migrate();

        using (AppDbContext context = database.CreateContext())
        {
            Assert.Equal("2026-11-002", Assert.Single(context.Batches).Code);
        }
    }

    [Fact]
    public void Journal_mode_of_the_database_file_is_wal()
    {
        using TestDatabase database = new();
        database.Migrate();

        using SqliteConnection connection = OpenRaw(database.DatabasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        Assert.Equal("wal", (string?)command.ExecuteScalar(), ignoreCase: true);
    }

    [Fact]
    public void Unique_index_of_batch_code_rejects_a_duplicate()
    {
        using TestDatabase database = new();
        database.Migrate();

        using (AppDbContext context = database.CreateContext())
        {
            context.Batches.Add(NewBatch("2026-11-002", BatchStatus.LISTO));
            context.SaveChanges();
        }

        using (AppDbContext context = database.CreateContext())
        {
            context.Batches.Add(NewBatch("2026-11-002", BatchStatus.LISTO));

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }

    [Fact]
    public void Unique_index_of_recipient_rejects_a_duplicate_cedula_in_the_same_batch()
    {
        using TestDatabase database = new();
        database.Migrate();
        int batchId = InsertBatch(database);

        using (AppDbContext context = database.CreateContext())
        {
            context.BatchRecipients.Add(NewRecipient(batchId, "12345678"));
            context.SaveChanges();
        }

        using (AppDbContext context = database.CreateContext())
        {
            context.BatchRecipients.Add(NewRecipient(batchId, "12345678"));

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }

    [Fact]
    public void Email_configuration_rejects_a_second_row()
    {
        using TestDatabase database = new();
        database.Migrate();

        using (AppDbContext context = database.CreateContext())
        {
            context.EmailConfigurations.Add(new EmailConfiguration { Id = 1, UpdatedAtUtc = SampleUtc });
            context.SaveChanges();
        }

        using (AppDbContext context = database.CreateContext())
        {
            context.EmailConfigurations.Add(new EmailConfiguration { Id = 2, UpdatedAtUtc = SampleUtc });

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }

    [Fact]
    public void Enums_are_stored_as_text_with_their_name_and_read_back()
    {
        using TestDatabase database = new();
        database.Migrate();

        int batchId;
        using (AppDbContext context = database.CreateContext())
        {
            Batch batch = NewBatch("2026-11-002", BatchStatus.LISTO_PARA_REANUDAR);
            BatchExecution execution = new()
            {
                Number = 1,
                Status = BatchStatus.EJECUTANDO,
                StartedAtUtc = SampleUtc,
                Phases = { new PhaseExecution { Phase = 1, Status = PhaseStatus.ESPERANDO_CONFIRMACION } },
            };
            batch.Executions.Add(execution);

            BatchRecipient recipient = NewRecipient(0, "12345678");
            recipient.PdfStatus = PdfStatus.GENERADO;
            recipient.SendStatus = SendStatus.INCIERTO;
            batch.Recipients.Add(recipient);

            context.Batches.Add(batch);
            context.SaveChanges();
            batchId = batch.Id;
        }

        Assert.Equal("LISTO_PARA_REANUDAR", ReadText(database.DatabasePath, "Batch", "Status"));
        Assert.Equal("EJECUTANDO", ReadText(database.DatabasePath, "BatchExecution", "Status"));
        Assert.Equal("ESPERANDO_CONFIRMACION", ReadText(database.DatabasePath, "PhaseExecution", "Status"));
        Assert.Equal("GENERADO", ReadText(database.DatabasePath, "BatchRecipient", "PdfStatus"));
        Assert.Equal("INCIERTO", ReadText(database.DatabasePath, "BatchRecipient", "SendStatus"));

        using AppDbContext reloaded = database.CreateContext();
        Batch stored = Assert.Single(reloaded.Batches
            .Include(batch => batch.Executions)
            .Include(batch => batch.Recipients)
            .Where(batch => batch.Id == batchId));
        Assert.Equal(BatchStatus.LISTO_PARA_REANUDAR, stored.Status);
        Assert.Equal(BatchStatus.EJECUTANDO, Assert.Single(stored.Executions).Status);
        Assert.Equal(PdfStatus.GENERADO, Assert.Single(stored.Recipients).PdfStatus);
        Assert.Equal(SendStatus.INCIERTO, Assert.Single(stored.Recipients).SendStatus);
    }

    [Fact]
    public void Dates_are_stored_as_utc_iso8601_text()
    {
        using TestDatabase database = new();
        database.Migrate();

        using (AppDbContext context = database.CreateContext())
        {
            Batch batch = NewBatch("2026-11-002", BatchStatus.LISTO);
            batch.Executions.Add(new BatchExecution
            {
                Number = 1,
                Status = BatchStatus.EJECUTANDO,
                StartedAtUtc = SampleUtc,
                LastCheckpointAtUtc = SampleUtc,
            });
            context.Batches.Add(batch);
            context.SaveChanges();
        }

        const string Expected = "2026-11-06T12:00:00.0000000Z";

        Assert.Equal(Expected, ReadText(database.DatabasePath, "Batch", "CreatedAtUtc"));
        Assert.Equal(Expected, ReadText(database.DatabasePath, "BatchExecution", "LastCheckpointAtUtc"));
    }

    private static int InsertBatch(TestDatabase database)
    {
        using AppDbContext context = database.CreateContext();
        Batch batch = NewBatch("2026-11-002", BatchStatus.LISTO);
        context.Batches.Add(batch);
        context.SaveChanges();

        return batch.Id;
    }

    private static Batch NewBatch(string code, BatchStatus status) => new()
    {
        Code = code,
        PeriodYear = 2026,
        PeriodMonth = 11,
        Status = status,
        CreatedAtUtc = SampleUtc,
        CreatedBy = "prueba",
    };

    private static BatchRecipient NewRecipient(int batchId, string cedula) => new()
    {
        BatchId = batchId,
        Cedula = cedula,
        FullName = "Asociado de prueba",
        PdfStatus = PdfStatus.PENDIENTE,
        SendStatus = SendStatus.PENDIENTE,
        UpdatedAtUtc = SampleUtc,
    };

    private static IReadOnlyList<string> ReadTableNames(string databasePath)
    {
        using SqliteConnection connection = OpenRaw(databasePath);
        using SqliteCommand command = connection.CreateCommand();

        // La lista de tablas se lee del catálogo de SQLite: es la forma de comprobar que Migrate()
        // creó el esquema, y sólo se usa desde las pruebas.
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' " +
            "AND name <> '__EFMigrationsHistory' AND name NOT LIKE 'sqlite_%' " +
            "ORDER BY name;";

        using SqliteDataReader reader = command.ExecuteReader();
        List<string> names = new();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>
    /// Lectura directa del texto guardado: es la única forma de comprobar el formato real de la columna,
    /// y sólo se usa desde las pruebas.
    /// </summary>
    private static string? ReadText(string databasePath, string table, string column)
    {
        using SqliteConnection connection = OpenRaw(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT \"{column}\" FROM \"{table}\" LIMIT 1;";

        return command.ExecuteScalar() as string;
    }

    private static SqliteConnection OpenRaw(string databasePath)
    {
        SqliteConnection connection = new($"Data Source={databasePath}");
        connection.Open();

        return connection;
    }
}
