namespace SmartPay.Core.Payments.Idempotency;

public interface IIdempotencyStore
{
    /// <summary>Try to record an idempotency key; returns false if already used.</summary>
    Task<bool> TryAddAsync(string tenantId, string key, TimeSpan ttl, CancellationToken ct = default);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, DateTimeOffset> _keys = new();
    private readonly object _gate = new();

    public Task<bool> TryAddAsync(string tenantId, string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var k = tenantId + "::" + key;
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_keys.TryGetValue(k, out var exp) && exp > now)
                return Task.FromResult(false);
            _keys[k] = now.Add(ttl);
            return Task.FromResult(true);
        }
    }
}