using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using Microsoft.Extensions.Logging;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// §2.8 reglas 1 a 3: el identificador de lote es único y legible, y su formato definitivo está pendiente.
/// El patrón <c>2026-11-002</c> es sólo el ejemplo ilustrativo del spec, así que la prueba fija ese valor
/// provisional y el aviso en el log que lo declara pendiente.
/// </summary>
public sealed class BatchCodeGeneratorTests
{
    [Fact]
    public void Generate_uses_the_provisional_pattern_of_the_spec_example()
    {
        RecordingLogger<BatchCodeGenerator> logger = new();
        BatchCodeGenerator generator = new(logger);

        Assert.Equal("2026-11-002", generator.Generate(2026, 11, 2));
        Assert.Equal("2027-01-001", generator.Generate(2027, 1, 1));
    }

    [Fact]
    public void TryReadSequence_reads_the_sequence_back_for_the_same_period()
    {
        RecordingLogger<BatchCodeGenerator> logger = new();
        BatchCodeGenerator generator = new(logger);

        Assert.True(generator.TryReadSequence("2026-11-002", 2026, 11, out int sequence));
        Assert.Equal(2, sequence);

        Assert.False(generator.TryReadSequence("2026-11-002", 2026, 12, out _));
        Assert.False(generator.TryReadSequence("otro-formato", 2026, 11, out _));
    }

    [Fact]
    public void Generate_warns_once_that_the_format_is_pending_confirmation()
    {
        RecordingLogger<BatchCodeGenerator> logger = new();
        BatchCodeGenerator generator = new(logger);

        generator.Generate(2026, 11, 1);
        generator.Generate(2026, 11, 2);

        RecordingLogger<BatchCodeGenerator>.Entry warning = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning);

        Assert.Contains("provisional", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pendiente de confirmación", warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_rejects_an_invalid_period()
    {
        BatchCodeGenerator generator = new(new RecordingLogger<BatchCodeGenerator>());

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(2026, 13, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(2026, 11, 0));
    }
}
