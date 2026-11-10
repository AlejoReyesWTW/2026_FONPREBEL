using System.IO;
using FonPrebel.Infrastructure.FileStorage;
using FonPrebel.Infrastructure.Logging;
using Serilog.Core;

namespace FonPrebel.Tests;

/// <summary>
/// Fija el requisito S17: dos bitácoras por día en la carpeta <c>YYYY-MM-DD</c>, con todo en
/// <c>execution.log</c> y sólo los errores en <c>errors.log</c>, y con las propiedades del log
/// (lote, ejecución, fase, resultado) cuando existen.
/// </summary>
public sealed class FonPrebelLoggingTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Execution_log_receives_every_level_and_errors_log_only_errors()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        {
            logger.Information("Proceso iniciado");
            logger.Warning("Reintento de verificación");
            logger.Error("Fase 1 detenida");
        }

        string execution = File.ReadAllText(paths.ExecutionLogPath(today));
        string errors = File.ReadAllText(paths.ErrorLogPath(today));

        Assert.Contains("Proceso iniciado", execution);
        Assert.Contains("Reintento de verificación", execution);
        Assert.Contains("Fase 1 detenida", execution);

        Assert.Contains("Fase 1 detenida", errors);
        Assert.DoesNotContain("Proceso iniciado", errors);
        Assert.DoesNotContain("Reintento de verificación", errors);
    }

    /// <summary>
    /// La bitácora es para el analista y para el soporte: el ruido de las librerías en inglés no entra.
    /// Sin este recorte, EF Core escribía cada comando SQL y el host su propio ciclo de vida, y el día
    /// llegaba a decenas de miles de líneas que tapaban los mensajes propios.
    /// </summary>
    [Fact]
    public void Infrastructure_noise_never_reaches_the_analyst_log()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        {
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.EntityFrameworkCore.Database.Command")
                .Information("Executed DbCommand: SELECT Host FROM EmailConfiguration");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.Hosting.Lifetime")
                .Information("Application is shutting down...");
            logger.ForContext(Constants.SourceContextPropertyName, "FonPrebel.Application.Services.BatchService")
                .Information("Lote creado para el mes en curso");
        }

        string execution = File.ReadAllText(paths.ExecutionLogPath(today));

        Assert.Contains("Lote creado para el mes en curso", execution);
        Assert.DoesNotContain("Executed DbCommand", execution);
        Assert.DoesNotContain("Application is shutting down", execution);
    }

    /// <summary>El nivel se escribe en español: la bitácora la lee el analista, no un programador.</summary>
    [Fact]
    public void The_level_is_written_in_spanish()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = FonPrebelLogging.CreateConfiguration(paths).CreateLogger())
        {
            logger.Warning("El servidor de archivos no respondió");
        }

        string execution = File.ReadAllText(paths.ExecutionLogPath(today));

        Assert.Contains("| AVISO |", execution);
        Assert.DoesNotContain("| WARNING |", execution);
    }

    [Fact]
    public void Logs_land_in_the_day_folder_with_the_exact_file_names()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = CreateTestLogger(paths))
        {
            logger.Information("mensaje del día");
        }

        string folder = Path.Combine(paths.Root, "Logs", today.ToString("yyyy-MM-dd"));

        Assert.True(Directory.Exists(folder), $"La carpeta del día debe existir en '{folder}'.");
        Assert.True(File.Exists(Path.Combine(folder, "execution.log")));
        Assert.Single(Directory.GetFiles(Path.Combine(paths.Root, "Logs"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Enriched_properties_appear_when_they_exist_and_do_not_pollute_other_lines()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = CreateTestLogger(paths))
        {
            logger.Information("línea sin propiedades");

            logger
                .ForContext(LogPropertyNames.Lote, "2026-11-002")
                .ForContext(LogPropertyNames.Ejecucion, 7)
                .ForContext(LogPropertyNames.Fase, 2)
                .ForContext(LogPropertyNames.Resultado, "completado")
                .Information("cruce terminado");
        }

        string[] lines = File.ReadAllLines(paths.ExecutionLogPath(today));

        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain(LogPropertyNames.Lote, lines[0]);
        Assert.DoesNotContain(LogPropertyNames.Fase, lines[0]);
        Assert.Contains("Lote: 2026-11-002", lines[1]);
        Assert.Contains("Ejecucion: 7", lines[1]);
        Assert.Contains("Fase: 2", lines[1]);
        Assert.Contains("Resultado: completado", lines[1]);
    }

    [Fact]
    public void Every_line_starts_with_the_date_time_and_the_level()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = CreateTestLogger(paths))
        {
            logger.Error("fallo registrado");
        }

        string line = File.ReadAllLines(paths.ErrorLogPath(today)).Single();

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \| ERROR \| ", line);
    }

    private static Logger CreateTestLogger(AppPaths paths) =>
        FonPrebelLogging.CreateConfiguration(paths).CreateLogger();

    [Fact]
    public void The_ui_can_read_the_logs_while_the_application_is_writing_them()
    {
        AppPaths paths = new(_temp.Path);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        using (Logger logger = CreateTestLogger(paths))
        {
            logger.Error("error visible desde la pantalla Logs");

            IReadOnlyList<string> execution = LogFileReader.ReadLastLines(paths.ExecutionLogPath(today), 10);
            IReadOnlyList<string> errors = LogFileReader.ReadLastLines(paths.ErrorLogPath(today), 10);

            Assert.Contains(execution, line => line.Contains("error visible desde la pantalla Logs"));
            Assert.Contains(errors, line => line.Contains("error visible desde la pantalla Logs"));
        }
    }
}
