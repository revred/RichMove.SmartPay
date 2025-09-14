using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Resilience;

/// <summary>
/// Circuit breaker for external API calls using failure thresholds
/// Prevents cascade failures and enables graceful degradation
/// </summary>
public sealed partial class CircuitBreakerService
{
    private readonly IClock _clock;
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly CircuitBreakerOptions _options;
    private readonly ConcurrentDictionary<string, CircuitState> _circuits = new();

    public CircuitBreakerService(IClock clock, ILogger<CircuitBreakerService> logger, IOptions<CircuitBreakerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Execute operation with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(string circuitName, Func<Task<T>> operation, Func<Task<T>>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(circuitName);
        ArgumentNullException.ThrowIfNull(operation);

        var circuit = _circuits.GetOrAdd(circuitName, _ => new CircuitState());

        // Check circuit state outside lock to avoid await in lock
        bool isOpenAndShouldBlock = false;
        Func<Task<T>>? fallbackToExecute = null;

        lock (circuit.LockObject)
        {
            if (circuit.State == BreakerState.Open)
            {
                if (_clock.UtcNow < circuit.NextRetryTime)
                {
                    Log.CircuitOpen(_logger, circuitName, circuit.NextRetryTime!.Value);
                    isOpenAndShouldBlock = true;
                    fallbackToExecute = fallback;
                }
                else
                {
                    // Transition to half-open for retry
                    circuit.State = BreakerState.HalfOpen;
                    Log.CircuitHalfOpen(_logger, circuitName);
                }
            }
        }

        if (isOpenAndShouldBlock)
        {
            if (fallbackToExecute != null)
            {
                Log.FallbackExecuted(_logger, circuitName);
                return await fallbackToExecute();
            }

            throw new CircuitBreakerOpenException($"Circuit breaker '{circuitName}' is open until {circuit.NextRetryTime:O}");
        }

        try
        {
            var result = await operation();
            RecordSuccess(circuit, circuitName);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(circuit, circuitName, ex);

            if (fallback != null && circuit.State == BreakerState.Open)
            {
                Log.FallbackExecuted(_logger, circuitName);
                return await fallback();
            }

            throw;
        }
    }

    /// <summary>
    /// Get current circuit state
    /// </summary>
    public CircuitInfo GetCircuitInfo(string circuitName)
    {
        ArgumentNullException.ThrowIfNull(circuitName);

        if (!_circuits.TryGetValue(circuitName, out var circuit))
        {
            return new CircuitInfo(circuitName, BreakerState.Closed, 0, 0, null);
        }

        lock (circuit.LockObject)
        {
            return new CircuitInfo(
                circuitName,
                circuit.State,
                circuit.FailureCount,
                circuit.SuccessCount,
                circuit.State == BreakerState.Open ? circuit.NextRetryTime : null);
        }
    }

    /// <summary>
    /// Reset circuit breaker (for testing or manual intervention)
    /// </summary>
    public void ResetCircuit(string circuitName)
    {
        ArgumentNullException.ThrowIfNull(circuitName);

        if (_circuits.TryGetValue(circuitName, out var circuit))
        {
            lock (circuit.LockObject)
            {
                circuit.State = BreakerState.Closed;
                circuit.FailureCount = 0;
                circuit.SuccessCount = 0;
                circuit.NextRetryTime = null;
            }

            Log.CircuitReset(_logger, circuitName);
        }
    }

    private void RecordSuccess(CircuitState circuit, string circuitName)
    {
        lock (circuit.LockObject)
        {
            circuit.SuccessCount++;

            if (circuit.State == BreakerState.HalfOpen)
            {
                if (circuit.SuccessCount >= _options.SuccessThreshold)
                {
                    circuit.State = BreakerState.Closed;
                    circuit.FailureCount = 0;
                    circuit.SuccessCount = 0;
                    Log.CircuitClosed(_logger, circuitName);
                }
            }
            else if (circuit.State == BreakerState.Closed)
            {
                // Reset failure count on success
                circuit.FailureCount = 0;
            }
        }
    }

    private void RecordFailure(CircuitState circuit, string circuitName, Exception exception)
    {
        lock (circuit.LockObject)
        {
            circuit.FailureCount++;

            if (circuit.State == BreakerState.Closed && circuit.FailureCount >= _options.FailureThreshold)
            {
                circuit.State = BreakerState.Open;
                circuit.NextRetryTime = _clock.UtcNow.Add(_options.OpenDuration);
                Log.CircuitOpened(_logger, circuitName, circuit.FailureCount, circuit.NextRetryTime!.Value, exception);
            }
            else if (circuit.State == BreakerState.HalfOpen)
            {
                circuit.State = BreakerState.Open;
                circuit.NextRetryTime = _clock.UtcNow.Add(_options.OpenDuration);
                Log.CircuitOpenedFromHalfOpen(_logger, circuitName, circuit.NextRetryTime!.Value, exception);
            }
            else
            {
                Log.FailureRecorded(_logger, circuitName, circuit.FailureCount, _options.FailureThreshold, exception);
            }
        }
    }

    private sealed class CircuitState
    {
        public readonly object LockObject = new();
        public BreakerState State { get; set; } = BreakerState.Closed;
        public int FailureCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime? NextRetryTime { get; set; }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 5401, Level = LogLevel.Warning, Message = "Circuit breaker '{CircuitName}' is open, next retry at {NextRetryTime:O}")]
        public static partial void CircuitOpen(ILogger logger, string circuitName, DateTime nextRetryTime);

        [LoggerMessage(EventId = 5402, Level = LogLevel.Information, Message = "Circuit breaker '{CircuitName}' transitioned to half-open")]
        public static partial void CircuitHalfOpen(ILogger logger, string circuitName);

        [LoggerMessage(EventId = 5403, Level = LogLevel.Warning, Message = "Circuit breaker '{CircuitName}' opened after {FailureCount} failures, next retry at {NextRetryTime:O}")]
        public static partial void CircuitOpened(ILogger logger, string circuitName, int failureCount, DateTime nextRetryTime, Exception exception);

        [LoggerMessage(EventId = 5404, Level = LogLevel.Information, Message = "Circuit breaker '{CircuitName}' closed")]
        public static partial void CircuitClosed(ILogger logger, string circuitName);

        [LoggerMessage(EventId = 5405, Level = LogLevel.Warning, Message = "Circuit breaker '{CircuitName}' opened from half-open, next retry at {NextRetryTime:O}")]
        public static partial void CircuitOpenedFromHalfOpen(ILogger logger, string circuitName, DateTime nextRetryTime, Exception exception);

        [LoggerMessage(EventId = 5406, Level = LogLevel.Information, Message = "Circuit breaker '{CircuitName}' reset manually")]
        public static partial void CircuitReset(ILogger logger, string circuitName);

        [LoggerMessage(EventId = 5407, Level = LogLevel.Information, Message = "Fallback executed for circuit '{CircuitName}'")]
        public static partial void FallbackExecuted(ILogger logger, string circuitName);

        [LoggerMessage(EventId = 5408, Level = LogLevel.Debug, Message = "Failure recorded for circuit '{CircuitName}': {FailureCount}/{Threshold}")]
        public static partial void FailureRecorded(ILogger logger, string circuitName, int failureCount, int threshold, Exception exception);
    }
}

/// <summary>
/// Circuit breaker configuration options
/// </summary>
public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 2;
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum BreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Circuit breaker information
/// </summary>
public sealed record CircuitInfo(
    string Name,
    BreakerState State,
    int FailureCount,
    int SuccessCount,
    DateTime? NextRetryTime);

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException() : base() { }
    public CircuitBreakerOpenException(string message) : base(message) { }
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
}