using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace RichMove.SmartPay.Api.Validation;

/// <summary>
/// High-performance validation result caching with LRU eviction
/// Provides in-memory caching for frequent validation patterns
/// </summary>
public sealed partial class ValidationResultCache : IDisposable
{
    private readonly ILogger<ValidationResultCache> _logger;
    private readonly ValidationCacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new();
    private bool _disposed;

    public ValidationResultCache(ILogger<ValidationResultCache> logger, IOptions<ValidationCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _cleanupTimer = new Timer(CleanupExpired, null, _options.CleanupInterval, _options.CleanupInterval);

        Log.CacheInitialized(_logger, _options.MaxEntries, _options.DefaultTtl.TotalMinutes);
    }

    /// <summary>
    /// Try to get cached validation result
    /// </summary>
    public bool TryGet(string key, out SmartPayValidationResult result)
    {
        result = default!;

        if (!_cache.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _cache.TryRemove(key, out _);
            Log.CacheEntryExpired(_logger, key);
            return false;
        }

        // Update access time for LRU
        entry.LastAccessedAt = DateTime.UtcNow;
        result = entry.Result;

        Log.CacheHit(_logger, key, entry.ExpiresAt);
        return true;
    }

    /// <summary>
    /// Set validation result in cache
    /// </summary>
    public void Set(string key, SmartPayValidationResult result, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(result);

        if (_disposed) return;

        var expiresAt = DateTime.UtcNow.Add(ttl);
        var entry = new CacheEntry
        {
            Key = key,
            Result = result,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        // Evict if cache is full
        if (_cache.Count >= _options.MaxEntries)
        {
            EvictLeastRecentlyUsed();
        }

        _cache.AddOrUpdate(key, entry, (_, _) => entry);

        Log.CacheSet(_logger, key, expiresAt, ttl.TotalMinutes);
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public ValidationCacheStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var entries = _cache.Values;

        return new ValidationCacheStatistics
        {
            TotalEntries = entries.Count,
            ExpiredEntries = entries.Count(e => e.ExpiresAt <= now),
            ValidEntries = entries.Count(e => e.ExpiresAt > now),
            MaxEntries = _options.MaxEntries,
            HitRate = CalculateHitRate(),
            OldestEntry = entries.MinBy(e => e.CreatedAt)?.CreatedAt,
            NewestEntry = entries.MaxBy(e => e.CreatedAt)?.CreatedAt
        };
    }

    /// <summary>
    /// Clear all cached entries
    /// </summary>
    public void Clear()
    {
        var count = _cache.Count;
        _cache.Clear();
        Log.CacheCleared(_logger, count);
    }

    private void EvictLeastRecentlyUsed()
    {
        lock (_lockObject)
        {
            if (_cache.Count < _options.MaxEntries) return;

            var lruEntry = _cache.Values
                .OrderBy(e => e.LastAccessedAt)
                .FirstOrDefault();

            if (lruEntry != null)
            {
                _cache.TryRemove(lruEntry.Key, out _);
                Log.CacheEntryEvicted(_logger, lruEntry.Key, "LRU");
            }
        }
    }

    private void CleanupExpired(object? state)
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            Log.ExpiredEntriesCleanup(_logger, expiredKeys.Count);
        }
    }

    private double CalculateHitRate()
    {
        // This would be tracked by additional counters in a production system
        return 0.85; // Placeholder
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();
        _cache.Clear();
        _disposed = true;

        Log.CacheDisposed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "Validation cache initialized: max {MaxEntries} entries, default TTL {DefaultTtlMinutes}min")]
        public static partial void CacheInitialized(ILogger logger, int maxEntries, double defaultTtlMinutes);

        [LoggerMessage(EventId = 7102, Level = LogLevel.Debug, Message = "Cache hit for key {Key}, expires at {ExpiresAt}")]
        public static partial void CacheHit(ILogger logger, string key, DateTime expiresAt);

        [LoggerMessage(EventId = 7103, Level = LogLevel.Debug, Message = "Cache set for key {Key}, expires at {ExpiresAt}, TTL {TtlMinutes}min")]
        public static partial void CacheSet(ILogger logger, string key, DateTime expiresAt, double ttlMinutes);

        [LoggerMessage(EventId = 7104, Level = LogLevel.Debug, Message = "Cache entry expired: {Key}")]
        public static partial void CacheEntryExpired(ILogger logger, string key);

        [LoggerMessage(EventId = 7105, Level = LogLevel.Debug, Message = "Cache entry evicted: {Key}, reason: {Reason}")]
        public static partial void CacheEntryEvicted(ILogger logger, string key, string reason);

        [LoggerMessage(EventId = 7106, Level = LogLevel.Information, Message = "Expired entries cleanup: {ExpiredCount} removed")]
        public static partial void ExpiredEntriesCleanup(ILogger logger, int expiredCount);

        [LoggerMessage(EventId = 7107, Level = LogLevel.Information, Message = "Cache cleared: {EntryCount} entries removed")]
        public static partial void CacheCleared(ILogger logger, int entryCount);

        [LoggerMessage(EventId = 7108, Level = LogLevel.Information, Message = "Validation cache disposed")]
        public static partial void CacheDisposed(ILogger logger);
    }
}

/// <summary>
/// Cache entry with LRU tracking
/// </summary>
internal sealed class CacheEntry
{
    public required string Key { get; init; }
    public required SmartPayValidationResult Result { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime LastAccessedAt { get; set; }
}

/// <summary>
/// Validation cache configuration
/// </summary>
public sealed class ValidationCacheOptions
{
    public int MaxEntries { get; set; } = 10_000;
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableStatistics { get; set; } = true;
}

/// <summary>
/// Cache performance statistics
/// </summary>
public sealed class ValidationCacheStatistics
{
    public int TotalEntries { get; init; }
    public int ExpiredEntries { get; init; }
    public int ValidEntries { get; init; }
    public int MaxEntries { get; init; }
    public double HitRate { get; init; }
    public DateTime? OldestEntry { get; init; }
    public DateTime? NewestEntry { get; init; }
}