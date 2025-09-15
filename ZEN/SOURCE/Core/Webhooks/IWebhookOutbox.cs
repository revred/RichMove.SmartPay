namespace SmartPay.Core.Webhooks;

public record WebhookEvent(string Id, string TenantId, string Type, string Payload, Uri Destination, int Attempt = 0);

public interface IWebhookOutbox
{
    Task EnqueueAsync(WebhookEvent evt, CancellationToken ct = default);
    Task<WebhookEvent?> DequeueAsync(CancellationToken ct = default);
}

public interface IWebhookSigner
{
    string Sign(string payload, string secret);
}