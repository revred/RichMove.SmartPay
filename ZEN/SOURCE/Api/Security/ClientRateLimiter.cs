using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Security;

/// <summary>
/// Per-client rate limiting with configurable limits
/// Prevents abuse and ensures fair resource allocation
/// </summary>
public sealed partial class ClientRateLimiter
{
    private readonly IClock _clock;
    private readonly ILogger<ClientRateLimiter> _logger;
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, ClientBucket> _buckets = new();

    public ClientRateLimiter(IClock clock, ILogger<ClientRateLimiter> logger, IOptions<RateLimitOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Check if request is allowed for client
    /// </summary>
    public RateLimitResult CheckRequest(string clientId, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(endpoint);

        var bucket = _buckets.GetOrAdd(clientId, _ => new ClientBucket(_clock.UtcNow));
        var limit = GetLimitForEndpoint(endpoint);

        lock (bucket.LockObject)
        {
            var now = _clock.UtcNow;

            // Reset bucket if window has passed
            if (now >= bucket.WindowStart.AddMinutes(_options.WindowMinutes))
            {
                bucket.RequestCount = 0;
                bucket.WindowStart = now;
            }

            // Check if limit exceeded
            if (bucket.RequestCount >= limit)
            {
                Log.RateLimitExceeded(_logger, clientId, endpoint, bucket.RequestCount, limit);
                return new RateLimitResult(false, limit, bucket.RequestCount, GetRetryAfterSeconds(bucket));
            }

            // Allow request
            bucket.RequestCount++;
            var utilizationPercent = (bucket.RequestCount / (double)limit) * 100;

            if (utilizationPercent > 80)
            {
                Log.RateLimitHighUtilization(_logger, clientId, endpoint, utilizationPercent);
            }

            Log.RequestAllowed(_logger, clientId, endpoint, bucket.RequestCount, limit);
            return new RateLimitResult(true, limit, bucket.RequestCount, 0);
        }
    }

    /// <summary>
    /// Get current rate limit status for client
    /// </summary>
    public RateLimitStatus GetStatus(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        if (!_buckets.TryGetValue(clientId, out var bucket))
        {
            return new RateLimitStatus(clientId, 0, _options.DefaultLimit, DateTime.UtcNow);
        }

        lock (bucket.LockObject)
        {
            return new RateLimitStatus(clientId, bucket.RequestCount, _options.DefaultLimit, bucket.WindowStart);
        }
    }

    /// <summary>
    /// Reset rate limit for client (for testing or admin actions)
    /// </summary>
    public void ResetClient(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        _buckets.TryRemove(clientId, out _);
        Log.ClientRateLimitReset(_logger, clientId);
    }

    private int GetLimitForEndpoint(string endpoint)
    {
        return _options.EndpointLimits.GetValueOrDefault(endpoint, _options.DefaultLimit);
    }

    private int GetRetryAfterSeconds(ClientBucket bucket)
    {
        var windowEnd = bucket.WindowStart.AddMinutes(_options.WindowMinutes);
        var retryAfter = (int)(windowEnd - _clock.UtcNow).TotalSeconds;
        return Math.Max(0, retryAfter);
    }

    private sealed class ClientBucket
    {
        public readonly object LockObject = new();
        public int RequestCount { get; set; }
        public DateTime WindowStart { get; set; }

        public ClientBucket(DateTime windowStart)
        {
            WindowStart = windowStart;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "Request allowed for client {ClientId} on {Endpoint}: {Current}/{Limit}")]
        public static partial void RequestAllowed(ILogger logger, string clientId, string endpoint, int current, int limit);

        [LoggerMessage(EventId = 5302, Level = LogLevel.Warning, Message = "Rate limit exceeded for client {ClientId} on {Endpoint}: {Current}/{Limit}")]
        public static partial void RateLimitExceeded(ILogger logger, string clientId, string endpoint, int current, int limit);

        [LoggerMessage(EventId = 5303, Level = LogLevel.Warning, Message = "High rate limit utilization for client {ClientId} on {Endpoint}: {Utilization:F1}%")]
        public static partial void RateLimitHighUtilization(ILogger logger, string clientId, string endpoint, double utilization);

        [LoggerMessage(EventId = 5304, Level = LogLevel.Information, Message = "Rate limit reset for client {ClientId}")]
        public static partial void ClientRateLimitReset(ILogger logger, string clientId);
    }
}

/// <summary>
/// Rate limit configuration options
/// </summary>
public sealed class RateLimitOptions
{
    public int DefaultLimit { get; set; } = 100;
    public int WindowMinutes { get; set; } = 60;
    public Dictionary<string, int> EndpointLimits { get; init; } = new();
}

/// <summary>
/// Result of rate limit check
/// </summary>
public sealed record RateLimitResult(
    bool IsAllowed,
    int Limit,
    int CurrentCount,
    int RetryAfterSeconds);

/// <summary>
/// Current rate limit status for a client
/// </summary>
public sealed record RateLimitStatus(
    string ClientId,
    int CurrentCount,
    int Limit,
    DateTime WindowStart);