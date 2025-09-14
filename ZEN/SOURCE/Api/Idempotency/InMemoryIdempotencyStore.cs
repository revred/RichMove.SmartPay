using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Idempotency;

public sealed partial class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTime> _keys = new();
    private readonly IClock _clock;
    private readonly ILogger<InMemoryIdempotencyStore> _logger;
    private volatile bool _coldStartCleanupDone;

    public InMemoryIdempotencyStore(IClock clock, ILogger<InMemoryIdempotencyStore> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task<bool> TryPutAsync(string key, DateTime expiresUtc, CancellationToken ct = default)
    {
        EnsureColdStartCleanup();
        return Task.FromResult(_keys.TryAdd(key, expiresUtc));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        EnsureColdStartCleanup();

        if (_keys.TryGetValue(key, out var exp) && exp > _clock.UtcNow)
            return Task.FromResult(true);
        _keys.TryRemove(key, out _);
        return Task.FromResult(false);
    }

    private void EnsureColdStartCleanup()
    {
        if (_coldStartCleanupDone) return;

        lock (_keys)
        {
            if (_coldStartCleanupDone) return;

            var now = _clock.UtcNow;
            var expiredKeys = _keys.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();

            foreach (var expiredKey in expiredKeys)
            {
                _keys.TryRemove(expiredKey, out _);
            }

            Log.ColdStartCleanup(_logger, expiredKeys.Count);
            _coldStartCleanupDone = true;
        }
    }

    /// <summary>
    /// Clean up expired entries from the store
    /// </summary>
    public Task<int> CleanupExpiredAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        var cutoffTime = _clock.UtcNow - retentionPeriod;
        var expiredKeys = new List<string>();

        foreach (var kvp in _keys)
        {
            if (kvp.Value < cutoffTime)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        var cleanedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (_keys.TryRemove(key, out _))
            {
                cleanedCount++;
            }
        }

        Log.PeriodicCleanup(_logger, cleanedCount, retentionPeriod.TotalHours);
        return Task.FromResult(cleanedCount);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Cold-start cleanup: purged {ExpiredCount} expired idempotency keys")]
        public static partial void ColdStartCleanup(ILogger logger, int expiredCount);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Periodic cleanup: purged {ExpiredCount} keys older than {RetentionHours:F1} hours")]
        public static partial void PeriodicCleanup(ILogger logger, int expiredCount, double retentionHours);
    }
}