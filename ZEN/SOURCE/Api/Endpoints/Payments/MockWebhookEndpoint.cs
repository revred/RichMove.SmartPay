using System.Text.Json;
using FastEndpoints;
using SmartPay.Core.Notifications;
using SmartPay.Infrastructure.Payments;

namespace SmartPay.Api.Endpoints.Payments;

/// <summary>
/// Mock provider webhook receiver: expects header "MockPay-Signature" which is HMAC-SHA256(payload).
/// </summary>
public sealed class MockWebhookRequest
{
    public string Type { get; set; } = default!; // e.g., payment_intent.succeeded
    public string IntentId { get; set; } = default!;
    public string TenantId { get; set; } = "default";
}

public sealed class MockWebhookEndpoint(MockPayProvider mock, INotificationService notifier) : Endpoint<MockWebhookRequest>
{
    public override void Configure()
    {
        Post("/api/payments/webhook/mock");
        AllowAnonymous();
        Summary(s => s.Summary = "Mock provider webhook (HMAC protected) for E2E testing");
    }

    public override async Task HandleAsync(MockWebhookRequest req, CancellationToken ct)
    {
        var signature = HttpContext.Request.Headers["MockPay-Signature"].FirstOrDefault();
        string payload;
        HttpContext.Request.EnableBuffering();
        using (var reader = new StreamReader(HttpContext.Request.Body, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(ct);
            HttpContext.Request.Body.Position = 0;
        }

        if (!mock.VerifySignature(payload, signature, mock.SecretForDocs()))
        {
            await SendAsync(new { error = "invalid signature" }, 400, ct);
            return;
        }

        if (req.Type == "payment_intent.succeeded")
        {
            await notifier.PublishAsync(req.TenantId, "payment.intent.succeeded", new { intentId = req.IntentId }, ct);
        }

        await SendOkAsync(ct);
    }
}