using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RichMove.SmartPay.Api.Idempotency;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTime> _keys = new();

    public Task<bool> TryPutAsync(string key, DateTime expiresUtc, CancellationToken ct = default)
        => Task.FromResult(_keys.TryAdd(key, expiresUtc));

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_keys.TryGetValue(key, out var exp) && exp > DateTime.UtcNow)
            return Task.FromResult(true);
        _keys.TryRemove(key, out _);
        return Task.FromResult(false);
    }
}