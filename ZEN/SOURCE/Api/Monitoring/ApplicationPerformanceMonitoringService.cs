using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;

namespace RichMove.SmartPay.Api.Monitoring;

public sealed partial class ApplicationPerformanceMonitoringService : IHostedService, IDisposable
{
    private readonly ILogger<ApplicationPerformanceMonitoringService> _logger;
    private readonly ApmOptions _options;
    private readonly Timer _collectionTimer;
    private readonly Meter _meter;

    // Performance metrics
    private readonly Counter<long> _requestCount;
    private readonly Histogram<double> _requestDuration;
    private readonly Gauge<long> _activeConnections;
    private readonly Gauge<double> _memoryUsage;
    private readonly Gauge<double> _cpuUsage;
    private readonly Counter<long> _errorCount;
    private readonly Histogram<double> _garbageCollectionTime;

    // APM data collection
    private readonly ConcurrentQueue<PerformanceTrace> _traces;
    private readonly ConcurrentDictionary<string, EndpointMetrics> _endpointMetrics;
    private readonly ConcurrentDictionary<string, TransactionTrace> _activeTransactions;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Process _currentProcess;

    public ApplicationPerformanceMonitoringService(
        ILogger<ApplicationPerformanceMonitoringService> logger,
        IOptions<ApmOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _traces = new ConcurrentQueue<PerformanceTrace>();
        _endpointMetrics = new ConcurrentDictionary<string, EndpointMetrics>();
        _activeTransactions = new ConcurrentDictionary<string, TransactionTrace>();
        _currentProcess = Process.GetCurrentProcess();

        _meter = new Meter("richmove.smartpay.apm");

        _requestCount = _meter.CreateCounter<long>(
            "richmove_smartpay_requests_total",
            "requests",
            "Total number of HTTP requests");

        _requestDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_request_duration_seconds",
            "seconds",
            "HTTP request duration in seconds");

        _activeConnections = _meter.CreateGauge<long>(
            "richmove_smartpay_active_connections",
            "connections",
            "Number of active connections");

        _memoryUsage = _meter.CreateGauge<double>(
            "richmove_smartpay_memory_usage_bytes",
            "bytes",
            "Memory usage in bytes");

        _cpuUsage = _meter.CreateGauge<double>(
            "richmove_smartpay_cpu_usage_percent",
            "percent",
            "CPU usage percentage");

        _errorCount = _meter.CreateCounter<long>(
            "richmove_smartpay_errors_total",
            "errors",
            "Total number of errors");

        _garbageCollectionTime = _meter.CreateHistogram<double>(
            "richmove_smartpay_gc_duration_seconds",
            "seconds",
            "Garbage collection duration in seconds");

        // Initialize CPU counter if on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch (Exception ex)
            {
                Log.CpuCounterInitializationFailed(_logger, ex);
            }
        }

        _collectionTimer = new Timer(CollectPerformanceData, null,
            TimeSpan.FromSeconds(10), _options.CollectionInterval);

        Log.ApmServiceInitialized(_logger, _options.CollectionInterval);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.ApmServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.ApmServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private async void CollectPerformanceData(object? state)
    {
        try
        {
            await CollectSystemMetrics();
            await CollectApplicationMetrics();
            await ProcessTraces();
            await AnalyzePerformanceTrends();
        }
        catch (Exception ex)
        {
            Log.PerformanceDataCollectionFailed(_logger, ex);
        }
    }

    private async Task CollectSystemMetrics()
    {
        await Task.Delay(5);

        // Memory metrics
        var totalMemory = GC.GetTotalMemory(false);
        _memoryUsage.Record(totalMemory);

        // GC metrics
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        // CPU metrics (Windows only)
        if (_cpuCounter != null)
        {
            try
            {
                var cpuPercent = _cpuCounter.NextValue();
                _cpuUsage.Record(cpuPercent);
            }
            catch (Exception ex)
            {
                Log.CpuMetricsCollectionFailed(_logger, ex);
            }
        }

        Log.SystemMetricsCollected(_logger, totalMemory, gen0Collections, gen1Collections, gen2Collections);
    }

    private async Task CollectApplicationMetrics()
    {
        await Task.Delay(5);

        // Active connections (simulated - would integrate with actual connection tracking)
        var activeConnections = _activeTransactions.Count;
        _activeConnections.Record(activeConnections);

        // Process endpoint metrics
        var endpointSummary = _endpointMetrics.Values
            .GroupBy(m => m.StatusCode >= 400 ? "error" : "success")
            .ToDictionary(g => g.Key, g => g.Sum(m => m.RequestCount));

        Log.ApplicationMetricsCollected(_logger, activeConnections, endpointSummary.Count);
    }

    private async Task ProcessTraces()
    {
        await Task.Delay(10);

        var processedCount = 0;
        while (_traces.TryDequeue(out var trace) && processedCount < 1000)
        {
            await ProcessTrace(trace);
            processedCount++;
        }

        if (processedCount > 0)
        {
            Log.TracesProcessed(_logger, processedCount);
        }
    }

    private async Task ProcessTrace(PerformanceTrace trace)
    {
        await Task.Delay(1);

        // Update endpoint metrics
        var endpointKey = $"{trace.Method}:{trace.Endpoint}";
        _endpointMetrics.AddOrUpdate(endpointKey,
            new EndpointMetrics
            {
                Endpoint = trace.Endpoint,
                Method = trace.Method,
                RequestCount = 1,
                TotalDuration = trace.Duration,
                StatusCode = trace.StatusCode,
                LastUpdated = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.RequestCount++;
                existing.TotalDuration += trace.Duration;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });

        // Record metrics
        _requestCount.Add(1,
            new KeyValuePair<string, object?>("method", trace.Method),
            new KeyValuePair<string, object?>("endpoint", trace.Endpoint),
            new KeyValuePair<string, object?>("status", trace.StatusCode.ToString()));

        _requestDuration.Record(trace.Duration.TotalSeconds,
            new KeyValuePair<string, object?>("method", trace.Method),
            new KeyValuePair<string, object?>("endpoint", trace.Endpoint));

        if (trace.StatusCode >= 400)
        {
            _errorCount.Add(1,
                new KeyValuePair<string, object?>("method", trace.Method),
                new KeyValuePair<string, object?>("endpoint", trace.Endpoint),
                new KeyValuePair<string, object?>("status", trace.StatusCode.ToString()));
        }
    }

    private async Task AnalyzePerformanceTrends()
    {
        await Task.Delay(15);

        var slowEndpoints = _endpointMetrics.Values
            .Where(m => m.RequestCount > 10)
            .Where(m => (m.TotalDuration.TotalMilliseconds / m.RequestCount) > _options.SlowRequestThresholdMs)
            .OrderByDescending(m => m.TotalDuration.TotalMilliseconds / m.RequestCount)
            .Take(5)
            .ToList();

        if (slowEndpoints.Count > 0)
        {
            Log.SlowEndpointsDetected(_logger, slowEndpoints.Count);
        }

        var errorEndpoints = _endpointMetrics.Values
            .Where(m => m.StatusCode >= 400 && m.RequestCount > 5)
            .OrderByDescending(m => m.RequestCount)
            .Take(5)
            .ToList();

        if (errorEndpoints.Count > 0)
        {
            Log.ErrorEndpointsDetected(_logger, errorEndpoints.Count);
        }
    }

    public void RecordRequest(string method, string endpoint, TimeSpan duration, int statusCode)
    {
        var trace = new PerformanceTrace
        {
            Id = Guid.NewGuid().ToString(),
            Method = method,
            Endpoint = endpoint,
            Duration = duration,
            StatusCode = statusCode,
            Timestamp = DateTime.UtcNow
        };

        _traces.Enqueue(trace);
    }

    public string StartTransaction(string name, string type = "request")
    {
        var transactionId = Guid.NewGuid().ToString();
        var transaction = new TransactionTrace
        {
            Id = transactionId,
            Name = name,
            Type = type,
            StartTime = DateTime.UtcNow,
            IsActive = true
        };

        _activeTransactions[transactionId] = transaction;
        return transactionId;
    }

    public void EndTransaction(string transactionId, bool success = true, string? errorMessage = null)
    {
        if (_activeTransactions.TryRemove(transactionId, out var transaction))
        {
            transaction.EndTime = DateTime.UtcNow;
            transaction.IsActive = false;
            transaction.Success = success;
            transaction.ErrorMessage = errorMessage;

            var duration = transaction.EndTime - transaction.StartTime;

            Log.TransactionCompleted(_logger, transaction.Name, transaction.Type,
                duration.TotalMilliseconds, success);
        }
    }

    public ApmStatus GetApmStatus()
    {
        return new ApmStatus
        {
            CollectedAt = DateTime.UtcNow,
            TotalTraces = _traces.Count,
            ActiveTransactions = _activeTransactions.Count,
            EndpointCount = _endpointMetrics.Count,
            MemoryUsage = GC.GetTotalMemory(false),
            IsHealthy = _traces.Count < _options.MaxQueuedTraces,
            TopEndpoints = _endpointMetrics.Values
                .OrderByDescending(m => m.RequestCount)
                .Take(10)
                .Select(m => new EndpointSummary
                {
                    Endpoint = m.Endpoint,
                    Method = m.Method,
                    RequestCount = m.RequestCount,
                    AvgDurationMs = m.TotalDuration.TotalMilliseconds / m.RequestCount,
                    StatusCode = m.StatusCode
                })
                .ToList()
        };
    }

    public void Dispose()
    {
        _collectionTimer?.Dispose();
        _cpuCounter?.Dispose();
        _currentProcess?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9101, Level = LogLevel.Information,
            Message = "APM service initialized (collection interval: {CollectionInterval})")]
        public static partial void ApmServiceInitialized(ILogger logger, TimeSpan collectionInterval);

        [LoggerMessage(EventId = 9102, Level = LogLevel.Information,
            Message = "APM service started")]
        public static partial void ApmServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9103, Level = LogLevel.Information,
            Message = "APM service stopped")]
        public static partial void ApmServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9104, Level = LogLevel.Warning,
            Message = "CPU counter initialization failed")]
        public static partial void CpuCounterInitializationFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9105, Level = LogLevel.Error,
            Message = "Performance data collection failed")]
        public static partial void PerformanceDataCollectionFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9106, Level = LogLevel.Debug,
            Message = "System metrics collected: Memory={MemoryBytes}, GC Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}")]
        public static partial void SystemMetricsCollected(ILogger logger, long memoryBytes, int gen0, int gen1, int gen2);

        [LoggerMessage(EventId = 9107, Level = LogLevel.Debug,
            Message = "Application metrics collected: ActiveConnections={ActiveConnections}, Endpoints={EndpointCount}")]
        public static partial void ApplicationMetricsCollected(ILogger logger, int activeConnections, int endpointCount);

        [LoggerMessage(EventId = 9108, Level = LogLevel.Debug,
            Message = "Processed {ProcessedCount} performance traces")]
        public static partial void TracesProcessed(ILogger logger, int processedCount);

        [LoggerMessage(EventId = 9109, Level = LogLevel.Warning,
            Message = "Detected {SlowEndpointCount} slow endpoints")]
        public static partial void SlowEndpointsDetected(ILogger logger, int slowEndpointCount);

        [LoggerMessage(EventId = 9110, Level = LogLevel.Warning,
            Message = "Detected {ErrorEndpointCount} endpoints with high error rates")]
        public static partial void ErrorEndpointsDetected(ILogger logger, int errorEndpointCount);

        [LoggerMessage(EventId = 9111, Level = LogLevel.Debug,
            Message = "Transaction completed: {Name} ({Type}) in {DurationMs}ms, Success={Success}")]
        public static partial void TransactionCompleted(ILogger logger, string name, string type,
            double durationMs, bool success);

        [LoggerMessage(EventId = 9112, Level = LogLevel.Warning,
            Message = "CPU metrics collection failed")]
        public static partial void CpuMetricsCollectionFailed(ILogger logger, Exception exception);
    }
}

// Supporting types
public sealed class ApmOptions
{
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxQueuedTraces { get; set; } = 10000;
    public double SlowRequestThresholdMs { get; set; } = 500;
    public bool EnableCpuMonitoring { get; set; } = true;
    public bool EnableMemoryMonitoring { get; set; } = true;
}

public sealed class PerformanceTrace
{
    public string Id { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class TransactionTrace
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class EndpointMetrics
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int StatusCode { get; set; }
    public DateTime LastUpdated { get; set; }
}

public sealed class ApmStatus
{
    public DateTime CollectedAt { get; set; }
    public int TotalTraces { get; set; }
    public int ActiveTransactions { get; set; }
    public int EndpointCount { get; set; }
    public long MemoryUsage { get; set; }
    public bool IsHealthy { get; set; }
    public List<EndpointSummary> TopEndpoints { get; set; } = [];
}

public sealed class EndpointSummary
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public double AvgDurationMs { get; set; }
    public int StatusCode { get; set; }
}