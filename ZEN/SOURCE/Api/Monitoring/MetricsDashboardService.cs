using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Monitoring;

public sealed partial class MetricsDashboardService : IHostedService, IDisposable
{
    private readonly ILogger<MetricsDashboardService> _logger;
    private readonly MetricsDashboardOptions _options;
    private readonly Timer _aggregationTimer;
    private readonly Timer _alertingTimer;
    private readonly Meter _meter;

    // Dashboard metrics
    private readonly Counter<long> _dashboardViews;
    private readonly Counter<long> _alertsFired;
    private readonly Gauge<long> _activeAlerts;
    private readonly Histogram<double> _queryDuration;

    // Data storage
    private readonly ConcurrentDictionary<string, MetricTimeSeries> _metrics;
    private readonly ConcurrentDictionary<string, Dashboard> _dashboards;
    private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
    private readonly ConcurrentQueue<Alert> _alertQueue;
    private readonly ConcurrentQueue<MetricDataPoint> _rawMetrics;

    public MetricsDashboardService(
        ILogger<MetricsDashboardService> logger,
        IOptions<MetricsDashboardOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _metrics = new ConcurrentDictionary<string, MetricTimeSeries>();
        _dashboards = new ConcurrentDictionary<string, Dashboard>();
        _alertRules = new ConcurrentDictionary<string, AlertRule>();
        _alertQueue = new ConcurrentQueue<Alert>();
        _rawMetrics = new ConcurrentQueue<MetricDataPoint>();

        _meter = new Meter("richmove.smartpay.dashboards");

        _dashboardViews = _meter.CreateCounter<long>(
            "richmove_smartpay_dashboard_views_total",
            "views",
            "Total number of dashboard views");

        _alertsFired = _meter.CreateCounter<long>(
            "richmove_smartpay_alerts_fired_total",
            "alerts",
            "Total number of alerts fired");

        _activeAlerts = _meter.CreateGauge<long>(
            "richmove_smartpay_active_alerts",
            "alerts",
            "Number of active alerts");

        _queryDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_query_duration_seconds",
            "seconds",
            "Time taken to execute metric queries");

        _aggregationTimer = new Timer(AggregateMetrics, null,
            TimeSpan.FromSeconds(30), _options.AggregationInterval);

        _alertingTimer = new Timer(EvaluateAlerts, null,
            TimeSpan.FromSeconds(15), _options.AlertingInterval);

        InitializeDefaultDashboards();
        InitializeDefaultAlerts();

        Log.MetricsDashboardServiceInitialized(_logger, _options.AggregationInterval, _options.AlertingInterval);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.MetricsDashboardServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.MetricsDashboardServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private void InitializeDefaultDashboards()
    {
        // System Overview Dashboard
        var systemDashboard = new Dashboard
        {
            Id = "system-overview",
            Name = "System Overview",
            Description = "High-level system metrics and health indicators",
            Panels = [
                new DashboardPanel
                {
                    Id = "requests-panel",
                    Title = "Request Rate",
                    Type = PanelType.Graph,
                    MetricQuery = "rate(richmove_smartpay_requests_total[5m])",
                    Unit = "requests/sec",
                    Position = new PanelPosition { X = 0, Y = 0, Width = 6, Height = 4 }
                },
                new DashboardPanel
                {
                    Id = "latency-panel",
                    Title = "Response Latency",
                    Type = PanelType.Graph,
                    MetricQuery = "histogram_quantile(0.95, richmove_smartpay_request_duration_seconds)",
                    Unit = "seconds",
                    Position = new PanelPosition { X = 6, Y = 0, Width = 6, Height = 4 }
                },
                new DashboardPanel
                {
                    Id = "error-rate-panel",
                    Title = "Error Rate",
                    Type = PanelType.SingleStat,
                    MetricQuery = "rate(richmove_smartpay_errors_total[5m])",
                    Unit = "errors/sec",
                    Position = new PanelPosition { X = 0, Y = 4, Width = 3, Height = 2 }
                },
                new DashboardPanel
                {
                    Id = "memory-panel",
                    Title = "Memory Usage",
                    Type = PanelType.Gauge,
                    MetricQuery = "richmove_smartpay_memory_usage_bytes",
                    Unit = "bytes",
                    Position = new PanelPosition { X = 3, Y = 4, Width = 3, Height = 2 }
                }
            ],
            Tags = ["system", "overview"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dashboards[systemDashboard.Id] = systemDashboard;

        // Business Metrics Dashboard
        var businessDashboard = new Dashboard
        {
            Id = "business-metrics",
            Name = "Business Metrics",
            Description = "Key business indicators and transaction metrics",
            Panels = [
                new DashboardPanel
                {
                    Id = "transactions-panel",
                    Title = "Transaction Volume",
                    Type = PanelType.Graph,
                    MetricQuery = "sum(rate(richmove_smartpay_transactions_total[5m]))",
                    Unit = "transactions/sec",
                    Position = new PanelPosition { X = 0, Y = 0, Width = 6, Height = 4 }
                },
                new DashboardPanel
                {
                    Id = "revenue-panel",
                    Title = "Revenue Rate",
                    Type = PanelType.Graph,
                    MetricQuery = "sum(rate(richmove_smartpay_transaction_value_total[5m]))",
                    Unit = "USD/sec",
                    Position = new PanelPosition { X = 6, Y = 0, Width = 6, Height = 4 }
                }
            ],
            Tags = ["business", "transactions"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dashboards[businessDashboard.Id] = businessDashboard;
    }

    private void InitializeDefaultAlerts()
    {
        // High Error Rate Alert
        var errorRateAlert = new AlertRule
        {
            Id = "high-error-rate",
            Name = "High Error Rate",
            Description = "Error rate exceeds 5% for more than 2 minutes",
            MetricQuery = "rate(richmove_smartpay_errors_total[5m]) / rate(richmove_smartpay_requests_total[5m]) * 100",
            Condition = AlertCondition.GreaterThan,
            Threshold = 5.0,
            Duration = TimeSpan.FromMinutes(2),
            Severity = AlertSeverity.Critical,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _alertRules[errorRateAlert.Id] = errorRateAlert;

        // High Memory Usage Alert
        var memoryAlert = new AlertRule
        {
            Id = "high-memory-usage",
            Name = "High Memory Usage",
            Description = "Memory usage exceeds 80% for more than 5 minutes",
            MetricQuery = "richmove_smartpay_memory_usage_bytes / (1024*1024*1024) * 100",
            Condition = AlertCondition.GreaterThan,
            Threshold = 80.0,
            Duration = TimeSpan.FromMinutes(5),
            Severity = AlertSeverity.Warning,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _alertRules[memoryAlert.Id] = memoryAlert;

        // High Response Latency Alert
        var latencyAlert = new AlertRule
        {
            Id = "high-response-latency",
            Name = "High Response Latency",
            Description = "95th percentile response time exceeds 1 second",
            MetricQuery = "histogram_quantile(0.95, richmove_smartpay_request_duration_seconds)",
            Condition = AlertCondition.GreaterThan,
            Threshold = 1.0,
            Duration = TimeSpan.FromMinutes(3),
            Severity = AlertSeverity.Warning,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _alertRules[latencyAlert.Id] = latencyAlert;
    }

    private async void AggregateMetrics(object? state)
    {
        try
        {
            await ProcessRawMetrics();
            await UpdateMetricTimeSeries();
        }
        catch (Exception ex)
        {
            Log.MetricAggregationFailed(_logger, ex);
        }
    }

    private async void EvaluateAlerts(object? state)
    {
        try
        {
            await EvaluateAlertRules();
            await UpdateActiveAlerts();
        }
        catch (Exception ex)
        {
            Log.AlertEvaluationFailed(_logger, ex);
        }
    }

    private async Task ProcessRawMetrics()
    {
        await Task.Delay(10);

        var processedCount = 0;
        var maxProcessCount = 5000;

        while (_rawMetrics.TryDequeue(out var dataPoint) && processedCount < maxProcessCount)
        {
            var timeSeriesKey = $"{dataPoint.MetricName}:{string.Join(",", dataPoint.Tags.Select(t => $"{t.Key}={t.Value}"))}";

            _metrics.AddOrUpdate(timeSeriesKey,
                new MetricTimeSeries
                {
                    MetricName = dataPoint.MetricName,
                    Tags = new Dictionary<string, string>(dataPoint.Tags),
                    DataPoints = [dataPoint],
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.DataPoints.Add(dataPoint);
                    existing.LastUpdated = DateTime.UtcNow;

                    // Keep only recent data points (last 24 hours)
                    var cutoff = DateTime.UtcNow.AddHours(-24);
                    existing.DataPoints = existing.DataPoints
                        .Where(dp => dp.Timestamp > cutoff)
                        .OrderBy(dp => dp.Timestamp)
                        .ToList();

                    return existing;
                });

            processedCount++;
        }

        if (processedCount > 0)
        {
            Log.MetricsProcessed(_logger, processedCount);
        }
    }

    private async Task UpdateMetricTimeSeries()
    {
        await Task.Delay(5);

        // Clean up old time series data
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var keysToRemove = _metrics
            .Where(kvp => kvp.Value.LastUpdated < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _metrics.TryRemove(key, out _);
        }

        Log.TimeSeriesUpdated(_logger, _metrics.Count, keysToRemove.Count);
    }

    private async Task EvaluateAlertRules()
    {
        await Task.Delay(20);

        foreach (var alertRule in _alertRules.Values.Where(ar => ar.IsEnabled))
        {
            var alertValue = await ExecuteMetricQuery(alertRule.MetricQuery);

            var shouldAlert = alertRule.Condition switch
            {
                AlertCondition.GreaterThan => alertValue > alertRule.Threshold,
                AlertCondition.LessThan => alertValue < alertRule.Threshold,
                AlertCondition.Equal => Math.Abs(alertValue - alertRule.Threshold) < 0.001,
                _ => false
            };

            if (shouldAlert)
            {
                await FireAlert(alertRule, alertValue);
            }
        }
    }

    private async Task<double> ExecuteMetricQuery(string query)
    {
        var queryStart = DateTime.UtcNow;
        await Task.Delay(10); // Simulate query execution

        // Simplified query evaluation - in real implementation would use PromQL parser
        var result = query.Contains("error") ? 3.5 :
                    query.Contains("memory") ? 75.2 :
                    query.Contains("latency") ? 0.45 :
                    42.0;

        var queryDuration = (DateTime.UtcNow - queryStart).TotalSeconds;
        _queryDuration.Record(queryDuration);

        return result;
    }

    private async Task FireAlert(AlertRule rule, double value)
    {
        await Task.Delay(5);

        var alert = new Alert
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            Severity = rule.Severity,
            Value = value,
            Threshold = rule.Threshold,
            FiredAt = DateTime.UtcNow,
            IsActive = true
        };

        _alertQueue.Enqueue(alert);

        _alertsFired.Add(1,
            new KeyValuePair<string, object?>("rule", rule.Id),
            new KeyValuePair<string, object?>("severity", rule.Severity.ToString()));

        Log.AlertFired(_logger, alert.Name, alert.Severity.ToString(), value, rule.Threshold);
    }

    private async Task UpdateActiveAlerts()
    {
        await Task.Delay(5);

        var activeAlertsCount = _alertQueue.Count(a => a.IsActive);
        _activeAlerts.Record(activeAlertsCount);
    }

    public void RecordMetric(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        var dataPoint = new MetricDataPoint
        {
            MetricName = metricName,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Tags = tags ?? new Dictionary<string, string>()
        };

        _rawMetrics.Enqueue(dataPoint);
    }

    public async Task<DashboardData> GetDashboard(string dashboardId)
    {
        var queryStart = DateTime.UtcNow;

        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            throw new ArgumentException($"Dashboard '{dashboardId}' not found");
        }

        var panelData = new List<PanelData>();

        foreach (var panel in dashboard.Panels)
        {
            var value = await ExecuteMetricQuery(panel.MetricQuery);
            panelData.Add(new PanelData
            {
                PanelId = panel.Id,
                Title = panel.Title,
                Value = value,
                Unit = panel.Unit,
                Type = panel.Type,
                Timestamp = DateTime.UtcNow
            });
        }

        _dashboardViews.Add(1,
            new KeyValuePair<string, object?>("dashboard", dashboardId));

        var queryDuration = (DateTime.UtcNow - queryStart).TotalSeconds;
        _queryDuration.Record(queryDuration);

        Log.DashboardViewed(_logger, dashboardId, panelData.Count, queryDuration * 1000);

        return new DashboardData
        {
            Dashboard = dashboard,
            Panels = panelData,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public List<Dashboard> GetDashboards()
    {
        return _dashboards.Values.ToList();
    }

    public List<AlertRule> GetAlertRules()
    {
        return _alertRules.Values.ToList();
    }

    public List<Alert> GetActiveAlerts()
    {
        return _alertQueue.Where(a => a.IsActive).ToList();
    }

    public MetricsDashboardStatus GetDashboardStatus()
    {
        return new MetricsDashboardStatus
        {
            DashboardCount = _dashboards.Count,
            AlertRuleCount = _alertRules.Count,
            ActiveAlertCount = _alertQueue.Count(a => a.IsActive),
            MetricTimeSeriesCount = _metrics.Count,
            QueuedMetrics = _rawMetrics.Count,
            IsHealthy = _rawMetrics.Count < _options.MaxQueuedMetrics,
            LastAggregation = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        _aggregationTimer?.Dispose();
        _alertingTimer?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9301, Level = LogLevel.Information,
            Message = "Metrics dashboard service initialized (aggregation: {AggregationInterval}, alerting: {AlertingInterval})")]
        public static partial void MetricsDashboardServiceInitialized(ILogger logger, TimeSpan aggregationInterval, TimeSpan alertingInterval);

        [LoggerMessage(EventId = 9302, Level = LogLevel.Information,
            Message = "Metrics dashboard service started")]
        public static partial void MetricsDashboardServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9303, Level = LogLevel.Information,
            Message = "Metrics dashboard service stopped")]
        public static partial void MetricsDashboardServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9304, Level = LogLevel.Debug,
            Message = "Processed {ProcessedCount} raw metrics")]
        public static partial void MetricsProcessed(ILogger logger, int processedCount);

        [LoggerMessage(EventId = 9305, Level = LogLevel.Debug,
            Message = "Updated time series: {ActiveSeries} active, {RemovedSeries} removed")]
        public static partial void TimeSeriesUpdated(ILogger logger, int activeSeries, int removedSeries);

        [LoggerMessage(EventId = 9306, Level = LogLevel.Warning,
            Message = "Alert fired: {AlertName} ({Severity}) - Value: {Value}, Threshold: {Threshold}")]
        public static partial void AlertFired(ILogger logger, string alertName, string severity, double value, double threshold);

        [LoggerMessage(EventId = 9307, Level = LogLevel.Debug,
            Message = "Dashboard viewed: {DashboardId} with {PanelCount} panels ({QueryDurationMs}ms)")]
        public static partial void DashboardViewed(ILogger logger, string dashboardId, int panelCount, double queryDurationMs);

        [LoggerMessage(EventId = 9308, Level = LogLevel.Error,
            Message = "Metric aggregation failed")]
        public static partial void MetricAggregationFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9309, Level = LogLevel.Error,
            Message = "Alert evaluation failed")]
        public static partial void AlertEvaluationFailed(ILogger logger, Exception exception);
    }
}

// Supporting types
public sealed class MetricsDashboardOptions
{
    public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan AlertingInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxQueuedMetrics { get; set; } = 50000;
    public TimeSpan DataRetention { get; set; } = TimeSpan.FromDays(7);
}

public sealed class MetricDataPoint
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public sealed class MetricTimeSeries
{
    public string MetricName { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public List<MetricDataPoint> DataPoints { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}

public sealed class Dashboard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DashboardPanel> Panels { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class DashboardPanel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public PanelType Type { get; set; }
    public string MetricQuery { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public PanelPosition Position { get; set; } = new();
}

public sealed class PanelPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class AlertRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MetricQuery { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public TimeSpan Duration { get; set; }
    public AlertSeverity Severity { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Alert
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public double Value { get; set; }
    public double Threshold { get; set; }
    public DateTime FiredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class DashboardData
{
    public Dashboard Dashboard { get; set; } = new();
    public List<PanelData> Panels { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}

public sealed class PanelData
{
    public string PanelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public PanelType Type { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class MetricsDashboardStatus
{
    public int DashboardCount { get; set; }
    public int AlertRuleCount { get; set; }
    public int ActiveAlertCount { get; set; }
    public int MetricTimeSeriesCount { get; set; }
    public int QueuedMetrics { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastAggregation { get; set; }
}

public enum PanelType
{
    Graph,
    SingleStat,
    Gauge,
    Table,
    Heatmap
}

public enum AlertCondition
{
    GreaterThan,
    LessThan,
    Equal
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}