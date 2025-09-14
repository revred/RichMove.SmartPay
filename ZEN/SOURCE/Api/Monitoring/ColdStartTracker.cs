using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RichMove.SmartPay.Api.Diagnostics;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Monitoring;

/// <summary>
/// Cold-start tracker - logs first-request latency once per deploy
/// Critical for monitoring application startup performance
/// </summary>
public sealed partial class ColdStartTracker
{
    private readonly IClock _clock;
    private readonly ILogger<ColdStartTracker> _logger;
    private readonly Stopwatch _startupTimer;
    private volatile bool _coldStartCompleted;
    private readonly object _lockObject = new();

    public ColdStartTracker(IClock clock, ILogger<ColdStartTracker> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _logger = logger;
        _startupTimer = Stopwatch.StartNew();

        Log.ColdStartTrackingInitiated(_logger, clock.UtcNow);
    }

    /// <summary>
    /// Record the first request processing completion
    /// Only logs once per application lifetime
    /// </summary>
    public void RecordFirstRequest(string endpoint, string method)
    {
        if (_coldStartCompleted) return;

        lock (_lockObject)
        {
            if (_coldStartCompleted) return;

            _startupTimer.Stop();
            var latencyMs = _startupTimer.ElapsedMilliseconds;

            // Use high-performance logging
            Log.ColdStartCompleted(_logger, latencyMs);

            Log.ColdStartDetailedCompletion(_logger, method, endpoint, latencyMs, _clock.UtcNow);

            _coldStartCompleted = true;
        }
    }

    /// <summary>
    /// Get current cold-start status
    /// </summary>
    public ColdStartStatus GetStatus()
    {
        return new ColdStartStatus
        {
            IsCompleted = _coldStartCompleted,
            ElapsedMs = _coldStartCompleted ? _startupTimer.ElapsedMilliseconds : _startupTimer.ElapsedMilliseconds,
            StartTime = _clock.UtcNow.AddMilliseconds(-_startupTimer.ElapsedMilliseconds)
        };
    }

    /// <summary>
    /// Force completion of cold-start tracking (for testing)
    /// </summary>
    internal void ForceCompletion()
    {
        if (!_coldStartCompleted)
        {
            RecordFirstRequest("test", "GET");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 4901, Level = LogLevel.Information, Message = "Cold-start tracking initiated at {StartTime}")]
        public static partial void ColdStartTrackingInitiated(ILogger logger, DateTime startTime);

        [LoggerMessage(EventId = 4902, Level = LogLevel.Information, Message = "Cold-start complete - First request processed in {LatencyMs}ms")]
        public static partial void ColdStartCompleted(ILogger logger, long latencyMs);

        [LoggerMessage(EventId = 4903, Level = LogLevel.Information, Message = "Cold-start complete - First {Method} {Endpoint} processed in {LatencyMs}ms at {CompletionTime}")]
        public static partial void ColdStartDetailedCompletion(ILogger logger, string method, string endpoint, long latencyMs, DateTime completionTime);
    }
}

/// <summary>
/// Cold-start tracking status
/// </summary>
public sealed record ColdStartStatus
{
    public bool IsCompleted { get; init; }
    public long ElapsedMs { get; init; }
    public DateTime StartTime { get; init; }
}