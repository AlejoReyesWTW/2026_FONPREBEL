using FonPrebel.Application.Interfaces;
using FonPrebel.Infrastructure.Email;

namespace FonPrebel.Tests.Connections;

/// <summary>
/// §2.14 y §2.10: la sonda de correo nunca afirma que el correo funciona. Sin servidor o puerto el
/// estado es desconocido con el motivo "sin configurar"; con configuración, el protocolo pendiente
/// mantiene el estado desconocido; sólo una comprobación real podría cambiarlo.
/// </summary>
public sealed class EmailConnectionProbeTests
{
    [Fact]
    public async Task Without_server_and_port_the_state_is_unknown_sin_configurar()
    {
        using ConnectionProbeTestContext context = new();
        EmailConnectionProbe probe = context.CreateEmailProbe();

        ConnectionProbeResult result = await probe.ProbeAsync();

        Assert.Equal(EmailConnectionProbe.DisplayName, result.TargetName);
        Assert.Equal(ConnectionProbeStatus.Unknown, result.Status);
        Assert.Equal("sin configurar", result.Reason);
        Assert.Contains("sin configurar", result.Message);
        Assert.Null(result.DurationMilliseconds);
        Assert.False(result.Responded);
    }

    [Fact]
    public async Task A_host_without_port_is_still_sin_configurar()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetEmailAsync("smtp.ejemplo.test", port: null);

        ConnectionProbeResult result = await context.CreateEmailProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Unknown, result.Status);
        Assert.Equal("sin configurar", result.Reason);
        Assert.NotEqual(ConnectionProbeStatus.Offline, result.Status);
    }

    [Fact]
    public async Task With_server_and_port_but_a_pending_protocol_it_does_not_claim_success()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetEmailAsync("smtp.ejemplo.test", 587);

        ConnectionProbeResult result = await context.CreateEmailProbe().ProbeAsync();

        Assert.Equal(ConnectionProbeStatus.Unknown, result.Status);
        Assert.Equal(EmailConnectionProbe.PendingProtocolReason, result.Reason);
        Assert.NotEqual(ConnectionProbeStatus.Online, result.Status);
        Assert.NotEqual(ConnectionProbeStatus.Slow, result.Status);
        Assert.Contains("pendiente", result.Message);
        Assert.False(result.Responded);
    }

    [Fact]
    public async Task The_extension_point_is_used_when_a_real_check_exists()
    {
        using ConnectionProbeTestContext context = new();
        await context.SetEmailAsync("smtp.ejemplo.test", 587);

        FakeEmailConnectionCheck check = new();
        ConnectionProbeResult result = await context.CreateEmailProbe(check).ProbeAsync();

        Assert.Equal("smtp.ejemplo.test", check.Received?.Host);
        Assert.Equal(587, check.Received?.Port);
        Assert.Equal(ConnectionProbeStatus.Online, result.Status);
        Assert.Equal(EmailConnectionProbe.Key, result.TargetKey);
        Assert.NotNull(result.DurationMilliseconds);
    }

    /// <summary>Comprobación de mentira que representa el mecanismo que TI todavía no definió.</summary>
    private sealed class FakeEmailConnectionCheck : IEmailConnectionCheck
    {
        public EmailSettings? Received { get; private set; }

        public Task<ConnectionProbeResult> CheckAsync(
            EmailSettings settings,
            CancellationToken cancellationToken = default)
        {
            Received = settings;

            return Task.FromResult(ConnectionProbeResult.Online(
                "mecanismo",
                "mecanismo",
                DateTimeOffset.UtcNow,
                durationMilliseconds: 12,
                "Responde: mecanismo de prueba."));
        }
    }
}
