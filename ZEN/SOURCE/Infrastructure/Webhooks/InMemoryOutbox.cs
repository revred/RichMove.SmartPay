using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SmartPay.Core.Webhooks;

namespace SmartPay.Infrastructure.Webhooks;

public sealed class InMemoryOutbox : IWebhookOutbox, IWebhookSigner
{
    private readonly ConcurrentQueue<WebhookEvent> _q = new();

    public Task EnqueueAsync(WebhookEvent evt, CancellationToken ct = default)
    {
        _q.Enqueue(evt);
        return Task.CompletedTask;
    }

    public Task<WebhookEvent?> DequeueAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_q.TryDequeue(out var evt) ? evt : null);
    }

    public string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}