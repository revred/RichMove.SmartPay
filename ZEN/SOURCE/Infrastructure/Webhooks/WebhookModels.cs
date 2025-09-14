using System.Text.Json;

namespace SmartPay.Infrastructure.Webhooks;

public sealed record WebhookEndpoint(string Name, string Url, string Secret, bool Active = true);

public sealed record WebhookEnvelope(
    string TenantId,
    string Topic,
    JsonElement Payload,
    long TimestampUnix);

public static class WebhookHeaders
{
    // Example: "t=1699970000, v1=0123abcd..."
    public const string Signature = "Richmove-Signature";
    public const string ContentType = "application/json";
}