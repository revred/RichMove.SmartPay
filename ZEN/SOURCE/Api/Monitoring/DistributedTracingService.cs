using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Monitoring;

public sealed partial class DistributedTracingService : IHostedService, IDisposable
{
    private readonly ILogger<DistributedTracingService> _logger;
    private readonly DistributedTracingOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly Timer _exportTimer;
    private readonly Meter _meter;

    // Tracing metrics
    private readonly Counter<long> _spanCount;
    private readonly Counter<long> _traceCount;
    private readonly Histogram<double> _spanDuration;
    private readonly Counter<long> _errorSpanCount;

    // Trace storage and export
    private readonly ConcurrentQueue<TraceSpan> _pendingSpans;
    private readonly ConcurrentDictionary<string, TraceContext> _activeTraces;
    private readonly ConcurrentDictionary<string, ServiceDependency> _serviceDependencies;

    // Sampling and filtering
    private readonly Random _random = new();
    private long _traceSequence = 0;

    public DistributedTracingService(
        ILogger<DistributedTracingService> logger,
        IOptions<DistributedTracingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _pendingSpans = new ConcurrentQueue<TraceSpan>();
        _activeTraces = new ConcurrentDictionary<string, TraceContext>();
        _serviceDependencies = new ConcurrentDictionary<string, ServiceDependency>();

        _activitySource = new ActivitySource(_options.ServiceName, _options.ServiceVersion);

        _meter = new Meter("richmove.smartpay.tracing");

        _spanCount = _meter.CreateCounter<long>(
            "richmove_smartpay_spans_total",
            "spans",
            "Total number of spans created");

        _traceCount = _meter.CreateCounter<long>(
            "richmove_smartpay_traces_total",
            "traces",
            "Total number of traces started");

        _spanDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_span_duration_seconds",
            "seconds",
            "Span duration in seconds");

        _errorSpanCount = _meter.CreateCounter<long>(
            "richmove_smartpay_error_spans_total",
            "spans",
            "Total number of error spans");

        _exportTimer = new Timer(ExportTraces, null,
            TimeSpan.FromSeconds(10), _options.ExportInterval);

        Log.DistributedTracingServiceInitialized(_logger, _options.ServiceName, _options.ExportInterval);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.DistributedTracingServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.DistributedTracingServiceStopped(_logger);
        return Task.CompletedTask;
    }

    public string StartTrace(string operationName, TraceKind kind = TraceKind.Server)
    {
        var traceId = GenerateTraceId();
        var spanId = GenerateSpanId();

        // Sampling decision
        if (!ShouldSample())
        {
            return traceId; // Return trace ID but don't create actual trace
        }

        var traceContext = new TraceContext
        {
            TraceId = traceId,
            ParentSpanId = null,
            ServiceName = _options.ServiceName,
            StartTime = DateTime.UtcNow,
            IsActive = true,
            IsSampled = true
        };

        _activeTraces[traceId] = traceContext;

        var rootSpanId = StartSpan(traceId, null, operationName, kind);
        traceContext.RootSpanId = rootSpanId;

        _traceCount.Add(1,
            new KeyValuePair<string, object?>("service", _options.ServiceName),
            new KeyValuePair<string, object?>("operation", operationName));

        Log.TraceStarted(_logger, traceId, operationName, kind.ToString());
        return traceId;
    }

    public string StartSpan(string traceId, string? parentSpanId, string operationName, TraceKind kind = TraceKind.Internal)
    {
        var spanId = GenerateSpanId();

        if (!_activeTraces.TryGetValue(traceId, out var traceContext) || !traceContext.IsSampled)
        {
            return spanId; // Return span ID but don't create actual span
        }

        var span = new TraceSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            OperationName = operationName,
            ServiceName = _options.ServiceName,
            Kind = kind,
            StartTime = DateTime.UtcNow,
            IsActive = true,
            Tags = new Dictionary<string, object>(),
            Events = []
        };

        // Create OpenTelemetry Activity
        using var activity = _activitySource.StartActivity(operationName);
        if (activity != null)
        {
            activity.SetTag("trace.id", traceId);
            activity.SetTag("span.id", spanId);
            activity.SetTag("service.name", _options.ServiceName);
            activity.SetTag("span.kind", kind.ToString().ToLowerInvariant());

            if (parentSpanId != null)
            {
                activity.SetTag("parent.span.id", parentSpanId);
            }
        }

        _pendingSpans.Enqueue(span);

        _spanCount.Add(1,
            new KeyValuePair<string, object?>("service", _options.ServiceName),
            new KeyValuePair<string, object?>("operation", operationName),
            new KeyValuePair<string, object?>("kind", kind.ToString()));

        Log.SpanStarted(_logger, spanId, traceId, operationName, kind.ToString());
        return spanId;
    }

    public void EndSpan(string traceId, string spanId, bool success = true, string? errorMessage = null)
    {
        if (!_activeTraces.TryGetValue(traceId, out var traceContext) || !traceContext.IsSampled)
        {
            return;
        }

        var endTime = DateTime.UtcNow;

        // Find and complete the span
        var spans = _pendingSpans.ToArray();
        var span = spans.FirstOrDefault(s => s.SpanId == spanId && s.TraceId == traceId);

        if (span != null)
        {
            span.EndTime = endTime;
            span.IsActive = false;
            span.Success = success;

            if (!success && errorMessage != null)
            {
                span.Tags["error"] = true;
                span.Tags["error.message"] = errorMessage;
                span.Events.Add(new SpanEvent
                {
                    Timestamp = endTime,
                    Name = "error",
                    Attributes = new Dictionary<string, object> { ["message"] = errorMessage }
                });

                _errorSpanCount.Add(1,
                    new KeyValuePair<string, object?>("service", _options.ServiceName),
                    new KeyValuePair<string, object?>("operation", span.OperationName));
            }

            var duration = endTime - span.StartTime;
            _spanDuration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("service", _options.ServiceName),
                new KeyValuePair<string, object?>("operation", span.OperationName),
                new KeyValuePair<string, object?>("success", success.ToString()));

            Log.SpanEnded(_logger, spanId, traceId, span.OperationName, duration.TotalMilliseconds, success);
        }
    }

    public void EndTrace(string traceId, bool success = true)
    {
        if (_activeTraces.TryRemove(traceId, out var traceContext))
        {
            traceContext.EndTime = DateTime.UtcNow;
            traceContext.IsActive = false;
            traceContext.Success = success;

            var duration = traceContext.EndTime - traceContext.StartTime;
            Log.TraceEnded(_logger, traceId, duration.TotalMilliseconds, success);
        }
    }

    public void AddSpanTag(string traceId, string spanId, string key, object value)
    {
        var spans = _pendingSpans.ToArray();
        var span = spans.FirstOrDefault(s => s.SpanId == spanId && s.TraceId == traceId);
        span?.Tags.TryAdd(key, value);
    }

    public void AddSpanEvent(string traceId, string spanId, string eventName, Dictionary<string, object>? attributes = null)
    {
        var spans = _pendingSpans.ToArray();
        var span = spans.FirstOrDefault(s => s.SpanId == spanId && s.TraceId == traceId);

        if (span != null)
        {
            span.Events.Add(new SpanEvent
            {
                Timestamp = DateTime.UtcNow,
                Name = eventName,
                Attributes = attributes ?? new Dictionary<string, object>()
            });
        }
    }

    public void RecordServiceDependency(string serviceName, string operationName, TimeSpan duration, bool success)
    {
        var dependencyKey = $"{serviceName}:{operationName}";
        _serviceDependencies.AddOrUpdate(dependencyKey,
            new ServiceDependency
            {
                ServiceName = serviceName,
                OperationName = operationName,
                CallCount = 1,
                TotalDuration = duration,
                SuccessCount = success ? 1 : 0,
                LastCall = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.CallCount++;
                existing.TotalDuration = existing.TotalDuration.Add(duration);
                if (success) existing.SuccessCount++;
                existing.LastCall = DateTime.UtcNow;
                return existing;
            });
    }

    private async void ExportTraces(object? state)
    {
        try
        {
            await ExportPendingTraces();
            await ExportServiceDependencies();
        }
        catch (Exception ex)
        {
            Log.TraceExportFailed(_logger, ex);
        }
    }

    private async Task ExportPendingTraces()
    {
        await Task.Delay(10);

        var exportedCount = 0;
        var maxExportCount = 1000;

        var spansToExport = new List<TraceSpan>();

        // Collect completed spans
        while (_pendingSpans.TryDequeue(out var span) && exportedCount < maxExportCount)
        {
            if (!span.IsActive)
            {
                spansToExport.Add(span);
                exportedCount++;
            }
            else
            {
                // Re-queue active spans
                _pendingSpans.Enqueue(span);
                break;
            }
        }

        if (spansToExport.Count > 0)
        {
            await SimulateTraceExport(spansToExport);
            Log.TracesExported(_logger, spansToExport.Count);
        }
    }

    private async Task ExportServiceDependencies()
    {
        await Task.Delay(5);

        var dependencySummary = _serviceDependencies.Values
            .Select(d => new
            {
                Service = d.ServiceName,
                Operation = d.OperationName,
                Calls = d.CallCount,
                AvgDurationMs = d.TotalDuration.TotalMilliseconds / d.CallCount,
                SuccessRate = (double)d.SuccessCount / d.CallCount
            })
            .OrderByDescending(d => d.Calls)
            .Take(10)
            .ToList();

        if (dependencySummary.Count > 0)
        {
            Log.ServiceDependenciesAnalyzed(_logger, dependencySummary.Count);
        }
    }

    private async Task SimulateTraceExport(List<TraceSpan> spans)
    {
        await Task.Delay(50);

        // In real implementation, this would export to:
        // - Jaeger
        // - Zipkin
        // - Azure Application Insights
        // - AWS X-Ray
        // - Google Cloud Trace

        var traceGroups = spans.GroupBy(s => s.TraceId).ToList();
        foreach (var traceGroup in traceGroups)
        {
            var traceJson = JsonSerializer.Serialize(traceGroup.ToList());
            // Export to configured tracing backend
        }
    }

    private bool ShouldSample()
    {
        return _random.NextDouble() < _options.SamplingRate;
    }

    private string GenerateTraceId()
    {
        var sequence = Interlocked.Increment(ref _traceSequence);
        return $"trace-{DateTime.UtcNow:yyyyMMddHHmmss}-{sequence:D6}";
    }

    private string GenerateSpanId()
    {
        return $"span-{Guid.NewGuid():N}[..8]";
    }

    public DistributedTracingStatus GetTracingStatus()
    {
        return new DistributedTracingStatus
        {
            ServiceName = _options.ServiceName,
            ServiceVersion = _options.ServiceVersion,
            ActiveTraces = _activeTraces.Count,
            PendingSpans = _pendingSpans.Count,
            SamplingRate = _options.SamplingRate,
            ExportInterval = _options.ExportInterval,
            IsHealthy = _pendingSpans.Count < _options.MaxPendingSpans,
            TopDependencies = _serviceDependencies.Values
                .OrderByDescending(d => d.CallCount)
                .Take(10)
                .Select(d => new DependencySummary
                {
                    ServiceName = d.ServiceName,
                    OperationName = d.OperationName,
                    CallCount = d.CallCount,
                    AvgDurationMs = d.TotalDuration.TotalMilliseconds / d.CallCount,
                    SuccessRate = (double)d.SuccessCount / d.CallCount
                })
                .ToList()
        };
    }

    public void Dispose()
    {
        _exportTimer?.Dispose();
        _activitySource?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9201, Level = LogLevel.Information,
            Message = "Distributed tracing service initialized for {ServiceName} (export interval: {ExportInterval})")]
        public static partial void DistributedTracingServiceInitialized(ILogger logger, string serviceName, TimeSpan exportInterval);

        [LoggerMessage(EventId = 9202, Level = LogLevel.Information,
            Message = "Distributed tracing service started")]
        public static partial void DistributedTracingServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9203, Level = LogLevel.Information,
            Message = "Distributed tracing service stopped")]
        public static partial void DistributedTracingServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9204, Level = LogLevel.Debug,
            Message = "Trace started: {TraceId} for {OperationName} ({Kind})")]
        public static partial void TraceStarted(ILogger logger, string traceId, string operationName, string kind);

        [LoggerMessage(EventId = 9205, Level = LogLevel.Debug,
            Message = "Span started: {SpanId} in trace {TraceId} for {OperationName} ({Kind})")]
        public static partial void SpanStarted(ILogger logger, string spanId, string traceId, string operationName, string kind);

        [LoggerMessage(EventId = 9206, Level = LogLevel.Debug,
            Message = "Span ended: {SpanId} in trace {TraceId} for {OperationName} ({DurationMs}ms, Success={Success})")]
        public static partial void SpanEnded(ILogger logger, string spanId, string traceId, string operationName, double durationMs, bool success);

        [LoggerMessage(EventId = 9207, Level = LogLevel.Debug,
            Message = "Trace ended: {TraceId} ({DurationMs}ms, Success={Success})")]
        public static partial void TraceEnded(ILogger logger, string traceId, double durationMs, bool success);

        [LoggerMessage(EventId = 9208, Level = LogLevel.Debug,
            Message = "Exported {ExportedCount} traces to backend")]
        public static partial void TracesExported(ILogger logger, int exportedCount);

        [LoggerMessage(EventId = 9209, Level = LogLevel.Debug,
            Message = "Analyzed {DependencyCount} service dependencies")]
        public static partial void ServiceDependenciesAnalyzed(ILogger logger, int dependencyCount);

        [LoggerMessage(EventId = 9210, Level = LogLevel.Error,
            Message = "Trace export failed")]
        public static partial void TraceExportFailed(ILogger logger, Exception exception);
    }
}

// Supporting types
public sealed class DistributedTracingOptions
{
    public string ServiceName { get; set; } = "smartpay-api";
    public string ServiceVersion { get; set; } = "1.0.0";
    public double SamplingRate { get; set; } = 0.1; // 10% sampling
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxPendingSpans { get; set; } = 10000;
    public string ExportEndpoint { get; set; } = "http://localhost:14268/api/traces";
}

public sealed class TraceSpan
{
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public TraceKind Kind { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; }
    public bool Success { get; set; } = true;
    public Dictionary<string, object> Tags { get; set; } = new();
    public List<SpanEvent> Events { get; set; } = [];
}

public sealed class SpanEvent
{
    public DateTime Timestamp { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Attributes { get; set; } = new();
}

public sealed class TraceContext
{
    public string TraceId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
    public string? RootSpanId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsSampled { get; set; }
    public bool Success { get; set; } = true;
}

public sealed class ServiceDependency
{
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public long CallCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public long SuccessCount { get; set; }
    public DateTime LastCall { get; set; }
}

public sealed class DistributedTracingStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceVersion { get; set; } = string.Empty;
    public int ActiveTraces { get; set; }
    public int PendingSpans { get; set; }
    public double SamplingRate { get; set; }
    public TimeSpan ExportInterval { get; set; }
    public bool IsHealthy { get; set; }
    public List<DependencySummary> TopDependencies { get; set; } = [];
}

public sealed class DependencySummary
{
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public long CallCount { get; set; }
    public double AvgDurationMs { get; set; }
    public double SuccessRate { get; set; }
}

public enum TraceKind
{
    Server,     // Inbound request
    Client,     // Outbound request
    Producer,   // Message producer
    Consumer,   // Message consumer
    Internal    // Internal operation
}