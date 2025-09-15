using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RichMove.SmartPay.Api.Monitoring;

public sealed partial class PrometheusMetricsService : IDisposable
{
    private readonly ILogger<PrometheusMetricsService> _logger;
    private readonly MetricsOptions _options;
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, object> _customCollectors;

    // Core metrics
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errorCounter;
    private readonly Gauge<long> _activeConnections;

    // Business metrics
    private readonly Counter<long> _fxQuoteCounter;
    private readonly Histogram<double> _fxQuoteDuration;
    private readonly Counter<long> _blockchainTxCounter;
    private readonly Histogram<double> _blockchainTxDuration;

    // Infrastructure metrics
    private readonly Gauge<long> _memoryUsage;
    private readonly Gauge<long> _cpuUsage;
    private readonly Counter<long> _gcCollections;

    public PrometheusMetricsService(
        ILogger<PrometheusMetricsService> logger,
        IOptions<MetricsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _customCollectors = new ConcurrentDictionary<string, object>();

        _meter = new Meter("richmove.smartpay.api", "1.0.0");

        // Initialize core metrics
        _requestCounter = _meter.CreateCounter<long>(
            "richmove_smartpay_requests_total",
            "requests",
            "Total number of HTTP requests");

        _requestDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_request_duration_seconds",
            "seconds",
            "HTTP request duration in seconds");

        _errorCounter = _meter.CreateCounter<long>(
            "richmove_smartpay_errors_total",
            "errors",
            "Total number of errors");

        _activeConnections = _meter.CreateGauge<long>(
            "richmove_smartpay_connections_active",
            "connections",
            "Number of active connections");

        // Initialize business metrics
        _fxQuoteCounter = _meter.CreateCounter<long>(
            "richmove_smartpay_fx_quotes_total",
            "quotes",
            "Total number of FX quotes generated");

        _fxQuoteDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_fx_quote_duration_seconds",
            "seconds",
            "FX quote generation duration in seconds");

        _blockchainTxCounter = _meter.CreateCounter<long>(
            "richmove_smartpay_blockchain_transactions_total",
            "transactions",
            "Total number of blockchain transactions");

        _blockchainTxDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_blockchain_transaction_duration_seconds",
            "seconds",
            "Blockchain transaction duration in seconds");

        // Initialize infrastructure metrics
        _memoryUsage = _meter.CreateGauge<long>(
            "richmove_smartpay_memory_usage_bytes",
            "bytes",
            "Current memory usage in bytes");

        _cpuUsage = _meter.CreateGauge<long>(
            "richmove_smartpay_cpu_usage_percent",
            "percent",
            "Current CPU usage percentage");

        _gcCollections = _meter.CreateCounter<long>(
            "richmove_smartpay_gc_collections_total",
            "collections",
            "Total number of garbage collections");

        Log.MetricsServiceInitialized(_logger, _options.EnableCustomCollectors);
    }

    // Core request metrics
    public void IncrementRequestCount(string endpoint, string method, int statusCode)
    {
        _requestCounter.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint),
                                new KeyValuePair<string, object?>("method", method),
                                new KeyValuePair<string, object?>("status_code", statusCode));
    }

    public void RecordRequestDuration(string endpoint, string method, double durationSeconds)
    {
        _requestDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method));
    }

    public void IncrementErrorCount(string errorType, string component, string severity = "error")
    {
        _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", errorType),
                             new KeyValuePair<string, object?>("component", component),
                             new KeyValuePair<string, object?>("severity", severity));
    }

    public void SetActiveConnections(long count)
    {
        _activeConnections.Record(count);
    }

    // Business operation metrics
    public void IncrementFxQuoteCount(string currencyPair, string status)
    {
        _fxQuoteCounter.Add(1, new KeyValuePair<string, object?>("currency_pair", currencyPair),
                               new KeyValuePair<string, object?>("status", status));
    }

    public void RecordFxQuoteDuration(string provider, string currencyPair, double durationSeconds)
    {
        _fxQuoteDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("currency_pair", currencyPair));
    }

    public void IncrementBlockchainTransactionCount(string network, string operation, string status)
    {
        _blockchainTxCounter.Add(1, new KeyValuePair<string, object?>("network", network),
                                    new KeyValuePair<string, object?>("operation", operation),
                                    new KeyValuePair<string, object?>("status", status));
    }

    public void RecordBlockchainTransactionDuration(string operation, string network, double durationSeconds)
    {
        _blockchainTxDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("network", network));
    }

    // Infrastructure metrics
    public void UpdateMemoryUsage(long bytes)
    {
        _memoryUsage.Record(bytes);
    }

    public void UpdateCpuUsage(long percentage)
    {
        _cpuUsage.Record(percentage);
    }

    public void IncrementGcCollectionCount(int generation)
    {
        _gcCollections.Add(1, new KeyValuePair<string, object?>("generation", generation));
    }

    // Custom collector support
    public void RegisterCustomCollector<T>(string name, Func<T> collector) where T : struct
    {
        if (!_options.EnableCustomCollectors)
        {
            Log.CustomCollectorIgnored(_logger, name);
            return;
        }

        _customCollectors.TryAdd(name, collector);
        Log.CustomCollectorRegistered(_logger, name, typeof(T).Name);
    }

    public async Task<Dictionary<string, object>> CollectAllMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["memory_usage_mb"] = GC.GetTotalMemory(false) / (1024 * 1024),
            ["gc_gen0_collections"] = GC.CollectionCount(0),
            ["gc_gen1_collections"] = GC.CollectionCount(1),
            ["gc_gen2_collections"] = GC.CollectionCount(2)
        };

        // Collect custom metrics
        if (_options.EnableCustomCollectors)
        {
            foreach (var collector in _customCollectors)
            {
                try
                {
                    if (collector.Value is Func<int> intCollector)
                        metrics[collector.Key] = intCollector();
                    else if (collector.Value is Func<long> longCollector)
                        metrics[collector.Key] = longCollector();
                    else if (collector.Value is Func<double> doubleCollector)
                        metrics[collector.Key] = doubleCollector();
                    else if (collector.Value is Func<Task<object>> asyncCollector)
                        metrics[collector.Key] = await asyncCollector();
                }
                catch (Exception ex)
                {
                    Log.CustomCollectorFailed(_logger, collector.Key, ex);
                }
            }
        }

        return metrics;
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 8601, Level = LogLevel.Information,
            Message = "Prometheus metrics service initialized (custom collectors: {EnableCustomCollectors})")]
        public static partial void MetricsServiceInitialized(ILogger logger, bool enableCustomCollectors);

        [LoggerMessage(EventId = 8602, Level = LogLevel.Debug,
            Message = "Custom collector registered: {Name} ({Type})")]
        public static partial void CustomCollectorRegistered(ILogger logger, string name, string type);

        [LoggerMessage(EventId = 8603, Level = LogLevel.Warning,
            Message = "Custom collector ignored (disabled): {Name}")]
        public static partial void CustomCollectorIgnored(ILogger logger, string name);

        [LoggerMessage(EventId = 8604, Level = LogLevel.Error,
            Message = "Custom collector failed: {Name}")]
        public static partial void CustomCollectorFailed(ILogger logger, string name, Exception exception);
    }
}

public sealed class MetricsOptions
{
    public bool EnableCustomCollectors { get; set; } = true;
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(15);
    public string MetricsEndpoint { get; set; } = "/metrics";
}

public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PrometheusMetricsService _metricsService;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(
        RequestDelegate next,
        PrometheusMetricsService metricsService,
        ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var endpoint = context.Request.Path.Value ?? "unknown";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var duration = stopwatch.Elapsed.TotalSeconds;

            _metricsService.IncrementRequestCount(endpoint, method, statusCode);
            _metricsService.RecordRequestDuration(endpoint, method, duration);

            if (statusCode >= 400)
            {
                var errorType = statusCode >= 500 ? "server_error" : "client_error";
                _metricsService.IncrementErrorCount(errorType, "api",
                    statusCode >= 500 ? "error" : "warning");
            }
        }
    }
}