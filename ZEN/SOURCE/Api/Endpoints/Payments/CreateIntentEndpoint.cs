using FastEndpoints;
using SmartPay.Api.Payments;
using SmartPay.Core.MultiTenancy;
using SmartPay.Core.Notifications;
using SmartPay.Core.Payments;
using SmartPay.Core.Payments.Idempotency;

namespace SmartPay.Api.Endpoints.Payments;

public sealed class CreateIntentRequest
{
    public string Currency { get; set; } = "GBP";
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

public sealed class CreateIntentResponse
{
    public string Provider { get; set; } = default!;
    public string IntentId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? ClientSecret { get; set; }
}

public sealed class CreateIntentEndpoint(
    IProviderRouter router,
    IIdempotencyStore idem,
    INotificationService notifier,
    ILogger<CreateIntentEndpoint> log) : Endpoint<CreateIntentRequest, CreateIntentResponse>
{
    public override void Configure()
    {
        Post("/api/payments/intent");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a payment intent with the primary provider";
            s.Description = "Idempotent via Idempotency-Key header; emits payment.intent.created event.";
        });
    }

    public override async Task HandleAsync(CreateIntentRequest req, CancellationToken ct)
    {
        if (req.Amount <= 0 || string.IsNullOrWhiteSpace(req.Currency))
        {
            await SendErrorsAsync(400, ct);
            return;
        }

        var tenantId = TenantContext.Current?.TenantId ?? "default";
        var idemKey = HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idemKey))
        {
            idemKey = Guid.NewGuid().ToString("N");
            HttpContext.Response.Headers.Append("Idempotency-Key", idemKey);
        }
        var ok = await idem.TryAddAsync(tenantId, idemKey, TimeSpan.FromHours(24), ct);
        if (!ok)
        {
            // Idempotent replay; return 200 with marker
            HttpContext.Response.Headers.Append("Idempotent-Replay", "true");
        }

        var provider = router.Resolve();
        var result = await provider.CreateIntentAsync(new PaymentIntentRequest(req.Currency, req.Amount, req.Reference, tenantId), ct);

        await notifier.PublishAsync(tenantId, "payment.intent.created", new
        {
            provider = result.Provider,
            intentId = result.IntentId,
            currency = req.Currency,
            amount = req.Amount,
            status = result.Status
        }, ct);

        await SendAsync(new CreateIntentResponse
        {
            Provider = result.Provider,
            IntentId = result.IntentId,
            Status = result.Status,
            ClientSecret = result.ClientSecret
        }, cancellation: ct);
    }
}