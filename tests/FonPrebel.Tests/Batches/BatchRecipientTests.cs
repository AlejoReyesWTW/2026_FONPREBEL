using FonPrebel.Application.Interfaces;
using FonPrebel.Application.Services;
using FonPrebel.Domain.Entities;
using FonPrebel.Domain.Enums;

namespace FonPrebel.Tests.Batches;

/// <summary>
/// S8 (§2.8 reglas 6, 11 y 12; Anexo F.9): destinatarios con cédula normalizada y clave única, y las
/// reglas de no duplicación consultables: un correo confirmado no se reenvía, un envío incierto no se
/// informa como enviado y un PDF generado no se regenera.
/// </summary>
public sealed class BatchRecipientTests
{
    [Fact]
    public async Task Register_normalizes_the_cedula_and_rejects_a_duplicate_key()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        await context.RegisterAsync(created.BatchId, "8.123.456");

        BatchOperationResult duplicate = await context.Recipients.RegisterAsync(
            created.BatchId,
            new[] { new RecipientRegistration("8123456", "Otro nombre") });

        Assert.Equal(BatchOutcome.DuplicateRecipient, duplicate.Outcome);

        IReadOnlyList<BatchRecipient> recipients = (await context.Recipients.ListAsync(created.BatchId)).Value!;
        BatchRecipient recipient = Assert.Single(recipients);
        Assert.Equal("8123456", recipient.Cedula);
        Assert.Equal("Jubilado de Prueba", recipient.FullName);
        Assert.Equal(PdfStatus.PENDIENTE, recipient.PdfStatus);
        Assert.Equal(SendStatus.PENDIENTE, recipient.SendStatus);
    }

    [Fact]
    public async Task Register_rejects_a_request_that_repeats_a_cedula_inside_itself()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        BatchOperationResult result = await context.Recipients.RegisterAsync(
            created.BatchId,
            new[]
            {
                new RecipientRegistration("8.123.456"),
                new RecipientRegistration("8123456"),
            });

        Assert.Equal(BatchOutcome.DuplicateRecipient, result.Outcome);
        Assert.Empty((await context.Recipients.ListAsync(created.BatchId)).Value!);
    }

    [Fact]
    public async Task Confirmed_recipients_are_excluded_from_resendable_and_uncertain_ones_are_not_sent()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        string confirmed = await context.RegisterAsync(created.BatchId, "1.111.111");
        string uncertain = await context.RegisterAsync(created.BatchId, "2.222.222");
        string pending = await context.RegisterAsync(created.BatchId, "3.333.333");
        string failed = await context.RegisterAsync(created.BatchId, "4.444.444");

        await MarkSentAsync(context, created.BatchId, confirmed);
        await MarkAsync(context, created.BatchId, uncertain, SendStatus.INCIERTO);
        await MarkAsync(context, created.BatchId, failed, SendStatus.FALLIDO);

        IReadOnlyList<BatchRecipient> resendable = (await context.Recipients.ListResendableAsync(created.BatchId)).Value!;
        IReadOnlyList<BatchRecipient> sent = (await context.Recipients.ListConfirmedSentAsync(created.BatchId)).Value!;

        Assert.Equal(new[] { failed, pending }.Order(StringComparer.Ordinal), resendable.Select(recipient => recipient.Cedula));
        Assert.DoesNotContain(resendable, recipient => recipient.Cedula == confirmed);
        Assert.DoesNotContain(resendable, recipient => recipient.Cedula == uncertain);

        Assert.Equal(new[] { confirmed }, sent.Select(recipient => recipient.Cedula));
        Assert.DoesNotContain(sent, recipient => recipient.Cedula == uncertain);

        // El incierto no se informa como enviado ni como pendiente de envío: queda para revisión (§2.8 regla 12).
        Assert.False(context.Policy.IsReportedAsSent(SendStatus.INCIERTO));
        Assert.True(context.Policy.IsReportedAsSent(SendStatus.ENVIADO));
    }

    [Fact]
    public async Task Generated_pdfs_are_excluded_from_regenerable()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        string generated = await context.RegisterAsync(created.BatchId, "1.111.111");
        string pending = await context.RegisterAsync(created.BatchId, "2.222.222");
        string failed = await context.RegisterAsync(created.BatchId, "3.333.333");

        BatchOperationResult generatedResult = await context.Recipients.UpdatePdfStatusAsync(
            new RecipientPdfUpdate(created.BatchId, generated, PdfStatus.GENERADO, @"C:\pdfs\1111111.pdf"));

        Assert.True(generatedResult.Succeeded, generatedResult.Detail);

        await UpdatePdfAsync(context, created.BatchId, failed, PdfStatus.FALLIDO);

        IReadOnlyList<BatchRecipient> regenerable =
            (await context.Recipients.ListRegenerablePdfAsync(created.BatchId)).Value!;

        Assert.Equal(new[] { failed, pending }.Order(StringComparer.Ordinal), regenerable.Select(recipient => recipient.Cedula));
        Assert.DoesNotContain(regenerable, recipient => recipient.Cedula == generated);
        Assert.False(context.Policy.CanRegeneratePdf(PdfStatus.GENERADO));
        Assert.True(context.Policy.CanRegeneratePdf(PdfStatus.PENDIENTE));
    }

    [Fact]
    public async Task Sending_requires_a_confirmation_from_the_sending_mechanism()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();
        string cedula = await context.RegisterAsync(created.BatchId, "8.123.456");

        BatchOperationResult withoutConfirmation = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, cedula, SendStatus.ENVIADO));

        Assert.Equal(BatchOutcome.ConfirmationRequired, withoutConfirmation.Outcome);
        Assert.Equal(
            SendStatus.PENDIENTE,
            Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!).SendStatus);

        await MarkSentAsync(context, created.BatchId, cedula);

        BatchRecipient recipient = Assert.Single((await context.Recipients.ListAsync(created.BatchId)).Value!);
        Assert.Equal(SendStatus.ENVIADO, recipient.SendStatus);
        Assert.NotNull(recipient.ConfirmedAtUtc);
    }

    [Fact]
    public async Task Updating_a_recipient_that_is_not_in_the_batch_is_reported()
    {
        using BatchTestContext context = new();
        BatchCreated created = await context.CreateAsync();

        BatchOperationResult pdf = await context.Recipients.UpdatePdfStatusAsync(
            new RecipientPdfUpdate(created.BatchId, "9999999", PdfStatus.GENERADO));

        BatchOperationResult send = await context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(created.BatchId, "9999999", SendStatus.FALLIDO));

        Assert.Equal(BatchOutcome.RecipientNotFound, pdf.Outcome);
        Assert.Equal(BatchOutcome.RecipientNotFound, send.Outcome);
    }

    private static Task MarkSentAsync(BatchTestContext context, int batchId, string cedula) =>
        context.Recipients.UpdateSendStatusAsync(
            new RecipientSendUpdate(batchId, cedula, SendStatus.ENVIADO, DateTime.UtcNow));

    private static Task<BatchOperationResult> MarkAsync(
        BatchTestContext context,
        int batchId,
        string cedula,
        SendStatus status) =>
        context.Recipients.UpdateSendStatusAsync(new RecipientSendUpdate(batchId, cedula, status));

    private static Task<BatchOperationResult> UpdatePdfAsync(
        BatchTestContext context,
        int batchId,
        string cedula,
        PdfStatus status) =>
        context.Recipients.UpdatePdfStatusAsync(new RecipientPdfUpdate(batchId, cedula, status));
}
