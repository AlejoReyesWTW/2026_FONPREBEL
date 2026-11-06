using System.IO;
using FonPrebel.Application.Validation;

namespace FonPrebel.Tests.Configuration;

/// <summary>
/// S6 y §2.9.2: una prueba por regla de validación de la configuración, incluido el puerto obligatorio
/// siempre, la condición del proxy y las rutas con comprobación real de existencia y escritura.
/// </summary>
public sealed class ConfigurationValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("58,7")]
    public void The_email_port_is_required_and_must_be_between_1_and_65535(string value)
    {
        ValidationResult result = EmailSettingsValidation.ValidatePort(value);

        Assert.False(result.IsValid);
        Assert.Contains(EmailSettingsValidation.PortParameter, result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("25")]
    [InlineData("587")]
    [InlineData("65535")]
    public void A_valid_email_port_is_accepted(string value)
    {
        Assert.True(EmailSettingsValidation.ValidatePort(value).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_email_host_and_security_are_required(string value)
    {
        ValidationResult host = EmailSettingsValidation.ValidateHost(value);
        ValidationResult security = EmailSettingsValidation.ValidateSecurity(value);

        Assert.False(host.IsValid);
        Assert.Contains(EmailSettingsValidation.HostParameter, host.Message, StringComparison.Ordinal);
        Assert.False(security.IsValid);
        Assert.Contains(EmailSettingsValidation.SecurityParameter, security.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("seguros@")]
    [InlineData("seguros@@ejemplo.test")]
    [InlineData("seguros ejemplo")]
    public void The_sender_must_have_an_address_format(string value)
    {
        ValidationResult result = EmailSettingsValidation.ValidateSender(value);

        Assert.False(result.IsValid);
        Assert.Contains(EmailSettingsValidation.SenderParameter, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_valid_sender_address_is_accepted()
    {
        Assert.True(EmailSettingsValidation.ValidateSender("seguros.fonprebel@ejemplo.test").IsValid);
    }

    [Fact]
    public void The_account_is_not_required_while_the_authentication_protocol_is_pending()
    {
        Assert.True(EmailSettingsValidation.ValidateAccount(string.Empty, requiredByProtocol: false).IsValid);
        Assert.True(EmailSettingsValidation.ValidateAccount(null, requiredByProtocol: false).IsValid);

        ValidationResult required = EmailSettingsValidation.ValidateAccount(string.Empty, requiredByProtocol: true);

        Assert.False(required.IsValid);
        Assert.Contains(EmailSettingsValidation.AccountParameter, required.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_send_timeout_must_be_a_positive_integer()
    {
        Assert.True(EmailSettingsValidation.ValidateSendTimeout("30").IsValid);
        Assert.False(EmailSettingsValidation.ValidateSendTimeout("0").IsValid);
        Assert.False(EmailSettingsValidation.ValidateSendTimeout(string.Empty).IsValid);
    }

    [Fact]
    public void The_proxy_text_is_optional_when_the_switch_is_off()
    {
        Assert.True(ProxySettingsValidation.ValidateText(enabled: false, text: string.Empty).IsValid);
        Assert.True(ProxySettingsValidation.ValidateText(enabled: false, text: null).IsValid);
    }

    [Fact]
    public void The_proxy_text_is_required_when_the_switch_is_on()
    {
        ValidationResult empty = ProxySettingsValidation.ValidateText(enabled: true, text: "  ");

        Assert.False(empty.IsValid);
        Assert.Contains(ProxySettingsValidation.ProxyTextParameter, empty.Message, StringComparison.Ordinal);
        Assert.True(ProxySettingsValidation.ValidateText(enabled: true, text: "http://proxy.ejemplo.test:8080").IsValid);
    }

    [Fact]
    public void The_routes_are_required()
    {
        ValidationResult route1 = FileRouteValidation.ValidateConsolidatedFile(string.Empty);
        ValidationResult route2 = FileRouteValidation.ValidatePolicyFolder(null);
        ValidationResult route3 = FileRouteValidation.ValidateAnalystFile("  ");
        ValidationResult route4 = FileRouteValidation.ValidatePdfFolder(string.Empty);

        Assert.False(route1.IsValid);
        Assert.Contains(FileRouteValidation.Route1Parameter, route1.Message, StringComparison.Ordinal);
        Assert.False(route2.IsValid);
        Assert.Contains(FileRouteValidation.Route2Parameter, route2.Message, StringComparison.Ordinal);
        Assert.False(route3.IsValid);
        Assert.Contains(FileRouteValidation.Route3Parameter, route3.Message, StringComparison.Ordinal);
        Assert.False(route4.IsValid);
        Assert.Contains(FileRouteValidation.Route4Parameter, route4.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_route_that_does_not_exist_is_invalid()
    {
        using TempDirectory temp = new();
        string missingFile = Path.Combine(temp.Path, "no-existe.xlsx");
        string missingFolder = Path.Combine(temp.Path, "no-existe");

        Assert.False(FileRouteValidation.ValidateConsolidatedFile(missingFile).IsValid);
        Assert.False(FileRouteValidation.ValidatePolicyFolder(missingFolder).IsValid);
        Assert.False(FileRouteValidation.ValidateAnalystFile(missingFile).IsValid);
        Assert.False(FileRouteValidation.ValidatePdfFolder(missingFolder).IsValid);
    }

    [Fact]
    public void The_existing_routes_with_write_permission_are_valid()
    {
        using TempDirectory temp = new();
        string consolidated = Path.Combine(temp.Path, "consolidado.xlsx");
        string analyst = Path.Combine(temp.Path, "analista.xlsx");
        string policies = Path.Combine(temp.Path, "polizas");
        string pdfs = Path.Combine(temp.Path, "pdfs");

        File.WriteAllText(consolidated, "consolidado");
        File.WriteAllText(analyst, "analista");
        Directory.CreateDirectory(policies);
        Directory.CreateDirectory(pdfs);

        Assert.True(FileRouteValidation.ValidateConsolidatedFile(consolidated).IsValid);
        Assert.True(FileRouteValidation.ValidatePolicyFolder(policies).IsValid);
        Assert.True(FileRouteValidation.ValidateAnalystFile(analyst).IsValid);
        Assert.True(FileRouteValidation.ValidatePdfFolder(pdfs).IsValid);
        Assert.Empty(Directory.GetFiles(pdfs));
    }

    [Fact]
    public void The_analyst_file_must_be_writable()
    {
        using TempDirectory temp = new();
        string analyst = Path.Combine(temp.Path, "analista.xlsx");
        File.WriteAllText(analyst, "analista");
        File.SetAttributes(analyst, FileAttributes.ReadOnly);

        try
        {
            ValidationResult result = FileRouteValidation.ValidateAnalystFile(analyst);

            Assert.False(result.IsValid);
            Assert.Contains(FileRouteValidation.Route3Parameter, result.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetAttributes(analyst, FileAttributes.Normal);
        }
    }

    [Fact]
    public void The_auto_check_interval_is_a_positive_integer()
    {
        Assert.True(ConcurrencySettingsValidation.ValidateAutoCheckMinutes("2").IsValid);
        Assert.False(ConcurrencySettingsValidation.ValidateAutoCheckMinutes("0").IsValid);
        Assert.False(ConcurrencySettingsValidation.ValidateAutoCheckMinutes(string.Empty).IsValid);
    }

    [Fact]
    public void The_slow_threshold_and_the_workers_are_required_and_not_invented()
    {
        ValidationResult threshold = ConcurrencySettingsValidation.ValidateSlowThresholdMs(string.Empty);
        ValidationResult pdfWorkers = ConcurrencySettingsValidation.ValidatePdfWorkers(null);
        ValidationResult emailWorkers = ConcurrencySettingsValidation.ValidateEmailWorkers(string.Empty);

        Assert.False(threshold.IsValid);
        Assert.Contains(ConcurrencySettingsValidation.SlowThresholdParameter, threshold.Message, StringComparison.Ordinal);
        Assert.False(pdfWorkers.IsValid);
        Assert.Contains(ConcurrencySettingsValidation.PdfWorkersParameter, pdfWorkers.Message, StringComparison.Ordinal);
        Assert.False(emailWorkers.IsValid);
        Assert.Contains(ConcurrencySettingsValidation.EmailWorkersParameter, emailWorkers.Message, StringComparison.Ordinal);

        Assert.True(ConcurrencySettingsValidation.ValidateSlowThresholdMs("1500").IsValid);
        Assert.True(ConcurrencySettingsValidation.ValidatePdfWorkers("1").IsValid);
        Assert.True(ConcurrencySettingsValidation.ValidateEmailWorkers("1").IsValid);
        Assert.False(ConcurrencySettingsValidation.ValidatePdfWorkers("0").IsValid);
        Assert.False(ConcurrencySettingsValidation.ValidateEmailWorkers("-2").IsValid);
    }

    [Fact]
    public void The_credential_edit_mode_requires_three_matching_fields()
    {
        ValidationResult missingPrevious = CredentialValidation.ValidateAll(string.Empty, "nueva", "nueva");
        ValidationResult missingNew = CredentialValidation.ValidateAll("anterior", string.Empty, string.Empty);
        ValidationResult mismatch = CredentialValidation.ValidateAll("anterior", "nueva", "otra");

        Assert.False(missingPrevious.IsValid);
        Assert.Contains(CredentialValidation.PreviousParameter, missingPrevious.Message, StringComparison.Ordinal);
        Assert.False(missingNew.IsValid);
        Assert.Contains(CredentialValidation.NewParameter, missingNew.Message, StringComparison.Ordinal);
        Assert.False(mismatch.IsValid);
        Assert.Contains(CredentialValidation.ConfirmationParameter, mismatch.Message, StringComparison.Ordinal);
        Assert.True(CredentialValidation.ValidateAll("anterior", "nueva", "nueva").IsValid);
    }

    [Fact]
    public void Every_invalid_message_starts_with_the_parameter_name()
    {
        (string Parameter, ValidationResult Result)[] invalid =
        {
            (EmailSettingsValidation.HostParameter, EmailSettingsValidation.ValidateHost(null)),
            (EmailSettingsValidation.PortParameter, EmailSettingsValidation.ValidatePort(null)),
            (EmailSettingsValidation.SecurityParameter, EmailSettingsValidation.ValidateSecurity(null)),
            (EmailSettingsValidation.SenderParameter, EmailSettingsValidation.ValidateSender(null)),
            (EmailSettingsValidation.SendTimeoutParameter, EmailSettingsValidation.ValidateSendTimeout(null)),
            (ProxySettingsValidation.ProxyTextParameter, ProxySettingsValidation.ValidateText(true, null)),
            (FileRouteValidation.Route1Parameter, FileRouteValidation.ValidateConsolidatedFile(null)),
            (FileRouteValidation.Route2Parameter, FileRouteValidation.ValidatePolicyFolder(null)),
            (FileRouteValidation.Route3Parameter, FileRouteValidation.ValidateAnalystFile(null)),
            (FileRouteValidation.Route4Parameter, FileRouteValidation.ValidatePdfFolder(null)),
            (ConcurrencySettingsValidation.AutoCheckParameter, ConcurrencySettingsValidation.ValidateAutoCheckMinutes(null)),
            (ConcurrencySettingsValidation.SlowThresholdParameter, ConcurrencySettingsValidation.ValidateSlowThresholdMs(null)),
            (ConcurrencySettingsValidation.PdfWorkersParameter, ConcurrencySettingsValidation.ValidatePdfWorkers(null)),
            (ConcurrencySettingsValidation.EmailWorkersParameter, ConcurrencySettingsValidation.ValidateEmailWorkers(null)),
        };

        Assert.All(invalid, entry =>
        {
            Assert.False(entry.Result.IsValid);
            Assert.StartsWith($"{entry.Parameter}:", entry.Result.Message, StringComparison.Ordinal);
        });
    }
}
