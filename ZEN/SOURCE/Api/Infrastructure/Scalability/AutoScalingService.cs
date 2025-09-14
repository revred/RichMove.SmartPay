using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api.Monitoring;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace RichMove.SmartPay.Api.Infrastructure.Scalability;

public sealed partial class AutoScalingService : IHostedService, IDisposable
{
    private readonly ILogger<AutoScalingService> _logger;
    private readonly ScalingOptions _options;
    private readonly PrometheusMetricsService _metricsService;
    private readonly Timer _evaluationTimer;
    private readonly ConcurrentQueue<ScalingEvent> _scalingHistory;
    private readonly Meter _meter;

    // Scaling metrics
    private readonly Counter<long> _scalingTriggers;
    private readonly Gauge<long> _currentLoad;
    private readonly Gauge<long> _targetReplicas;

    // Load tracking
    private readonly ConcurrentDictionary<string, LoadMetrics> _loadMetrics;
    private DateTime _lastScaleAction = DateTime.MinValue;

    public AutoScalingService(
        ILogger<AutoScalingService> logger,
        IOptions<ScalingOptions> options,
        PrometheusMetricsService metricsService)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _metricsService = metricsService;
        _scalingHistory = new ConcurrentQueue<ScalingEvent>();
        _loadMetrics = new ConcurrentDictionary<string, LoadMetrics>();

        _meter = new Meter("richmove.smartpay.autoscaling");

        _scalingTriggers = _meter.CreateCounter<long>(
            "richmove_smartpay_scaling_triggers_total",
            "triggers",
            "Total number of auto-scaling triggers");

        _currentLoad = _meter.CreateGauge<long>(
            "richmove_smartpay_current_load_percent",
            "percent",
            "Current system load percentage");

        _targetReplicas = _meter.CreateGauge<long>(
            "richmove_smartpay_target_replicas",
            "replicas",
            "Target number of replicas");

        _evaluationTimer = new Timer(EvaluateScaling, null,
            _options.EvaluationInterval, _options.EvaluationInterval);

        Log.AutoScalingServiceInitialized(_logger, _options.MinReplicas, _options.MaxReplicas);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.AutoScalingServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.AutoScalingServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private async void EvaluateScaling(object? state)
    {
        try
        {
            var metrics = await CollectCurrentMetrics();
            var decision = MakeScalingDecision(metrics);

            if (decision.ShouldScale)
            {
                await ExecuteScalingDecision(decision);
            }

            // Update scaling metrics
            _currentLoad.Record((long)metrics.CpuUtilization);
            _targetReplicas.Record(decision.TargetReplicas);

            Log.ScalingEvaluationCompleted(_logger, metrics.CpuUtilization, metrics.MemoryUtilization, decision.TargetReplicas);
        }
        catch (Exception ex)
        {
            Log.ScalingEvaluationFailed(_logger, ex);
        }
    }

    private async Task<SystemMetrics> CollectCurrentMetrics()
    {
        var allMetrics = await _metricsService.CollectAllMetricsAsync();

        var cpuUtilization = GetMetricValue(allMetrics, "cpu_usage_percent", 0.0);
        var memoryUtilization = (GetMetricValue(allMetrics, "memory_usage_mb", 0.0) / 1024.0) * 100; // Convert to percentage
        var activeConnections = GetMetricValue(allMetrics, "connections_active", 0.0);
        var requestRate = CalculateRequestRate();

        return new SystemMetrics
        {
            CpuUtilization = cpuUtilization,
            MemoryUtilization = memoryUtilization,
            ActiveConnections = (int)activeConnections,
            RequestRate = requestRate,
            Timestamp = DateTime.UtcNow
        };
    }

    private static double GetMetricValue(Dictionary<string, object> metrics, string key, double defaultValue)
    {
        if (metrics.TryGetValue(key, out var value))
        {
            return value switch
            {
                double d => d,
                long l => l,
                int i => i,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private double CalculateRequestRate()
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        // Calculate requests per minute from load metrics
        var recentRequests = _loadMetrics.Values
            .Where(m => m.Timestamp > oneMinuteAgo)
            .Sum(m => m.RequestCount);

        return recentRequests;
    }

    private ScalingDecision MakeScalingDecision(SystemMetrics metrics)
    {
        var currentReplicas = GetCurrentReplicas();
        var targetReplicas = currentReplicas;
        var shouldScale = false;
        var reason = "No scaling needed";

        // Check if enough time has passed since last scaling action
        var timeSinceLastScale = DateTime.UtcNow - _lastScaleAction;
        if (timeSinceLastScale < _options.CooldownPeriod)
        {
            return new ScalingDecision
            {
                ShouldScale = false,
                TargetReplicas = currentReplicas,
                Reason = $"Cooldown period active ({timeSinceLastScale.TotalSeconds:F0}s remaining)"
            };
        }

        // Scale up conditions
        if (metrics.CpuUtilization > _options.CpuThresholdUp ||
            metrics.MemoryUtilization > _options.MemoryThresholdUp ||
            metrics.RequestRate > _options.RequestRateThresholdUp)
        {
            targetReplicas = Math.Min(currentReplicas + 1, _options.MaxReplicas);
            if (targetReplicas > currentReplicas)
            {
                shouldScale = true;
                reason = $"Scale up: CPU={metrics.CpuUtilization:F1}%, Memory={metrics.MemoryUtilization:F1}%, Requests={metrics.RequestRate:F0}/min";
            }
        }
        // Scale down conditions
        else if (metrics.CpuUtilization < _options.CpuThresholdDown &&
                 metrics.MemoryUtilization < _options.MemoryThresholdDown &&
                 metrics.RequestRate < _options.RequestRateThresholdDown)
        {
            targetReplicas = Math.Max(currentReplicas - 1, _options.MinReplicas);
            if (targetReplicas < currentReplicas)
            {
                shouldScale = true;
                reason = $"Scale down: CPU={metrics.CpuUtilization:F1}%, Memory={metrics.MemoryUtilization:F1}%, Requests={metrics.RequestRate:F0}/min";
            }
        }

        return new ScalingDecision
        {
            ShouldScale = shouldScale,
            TargetReplicas = targetReplicas,
            Reason = reason,
            Metrics = metrics
        };
    }

    private async Task ExecuteScalingDecision(ScalingDecision decision)
    {
        var scalingEvent = new ScalingEvent
        {
            Timestamp = DateTime.UtcNow,
            FromReplicas = GetCurrentReplicas(),
            ToReplicas = decision.TargetReplicas,
            Reason = decision.Reason,
            Metrics = decision.Metrics
        };

        // Record scaling event
        _scalingHistory.Enqueue(scalingEvent);
        _scalingTriggers.Add(1,
            new KeyValuePair<string, object?>("direction", scalingEvent.ToReplicas > scalingEvent.FromReplicas ? "up" : "down"),
            new KeyValuePair<string, object?>("reason", "auto"));

        // In a real implementation, this would call Kubernetes API or cloud provider
        await SimulateScalingAction(decision);

        _lastScaleAction = DateTime.UtcNow;

        Log.ScalingActionExecuted(_logger, scalingEvent.FromReplicas, scalingEvent.ToReplicas, decision.Reason);

        // Cleanup old history
        while (_scalingHistory.Count > 100)
        {
            _scalingHistory.TryDequeue(out _);
        }
    }

    private static async Task SimulateScalingAction(ScalingDecision decision)
    {
        // Simulate time for scaling operation
        await Task.Delay(1000);

        // In production, this would:
        // 1. Call Kubernetes HPA API
        // 2. Update cloud provider auto-scaling groups
        // 3. Notify monitoring systems
    }

    private static int GetCurrentReplicas()
    {
        // In production, this would query Kubernetes or cloud provider
        // For simulation, return a value based on environment variable or default
        var replicasEnv = Environment.GetEnvironmentVariable("CURRENT_REPLICAS");
        return int.TryParse(replicasEnv, out var replicas) ? replicas : 1;
    }

    public void RecordLoadMetrics(string endpoint, int requestCount, double responseTime)
    {
        var key = endpoint;
        var metrics = new LoadMetrics
        {
            Endpoint = endpoint,
            RequestCount = requestCount,
            AverageResponseTime = responseTime,
            Timestamp = DateTime.UtcNow
        };

        _loadMetrics.AddOrUpdate(key, metrics, (_, _) => metrics);
    }

    public ScalingStatus GetScalingStatus()
    {
        var recentEvents = _scalingHistory.ToArray()
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();

        return new ScalingStatus
        {
            CurrentReplicas = GetCurrentReplicas(),
            MinReplicas = _options.MinReplicas,
            MaxReplicas = _options.MaxReplicas,
            LastScaleAction = _lastScaleAction,
            RecentEvents = recentEvents,
            IsInCooldown = DateTime.UtcNow - _lastScaleAction < _options.CooldownPeriod
        };
    }

    public void Dispose()
    {
        _evaluationTimer?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 8701, Level = LogLevel.Information,
            Message = "Auto-scaling service initialized (replicas: {MinReplicas}-{MaxReplicas})")]
        public static partial void AutoScalingServiceInitialized(ILogger logger, int minReplicas, int maxReplicas);

        [LoggerMessage(EventId = 8702, Level = LogLevel.Information,
            Message = "Auto-scaling service started")]
        public static partial void AutoScalingServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 8703, Level = LogLevel.Information,
            Message = "Auto-scaling service stopped")]
        public static partial void AutoScalingServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 8704, Level = LogLevel.Debug,
            Message = "Scaling evaluation: CPU={CpuUtilization:F1}%, Memory={MemoryUtilization:F1}%, Target={TargetReplicas}")]
        public static partial void ScalingEvaluationCompleted(ILogger logger, double cpuUtilization, double memoryUtilization, int targetReplicas);

        [LoggerMessage(EventId = 8705, Level = LogLevel.Error,
            Message = "Scaling evaluation failed")]
        public static partial void ScalingEvaluationFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 8706, Level = LogLevel.Warning,
            Message = "Scaling action executed: {FromReplicas} → {ToReplicas} ({Reason})")]
        public static partial void ScalingActionExecuted(ILogger logger, int fromReplicas, int toReplicas, string reason);
    }
}

public sealed class ScalingOptions
{
    public int MinReplicas { get; set; } = 1;
    public int MaxReplicas { get; set; } = 10;
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(2);

    public double CpuThresholdUp { get; set; } = 70.0;
    public double CpuThresholdDown { get; set; } = 30.0;
    public double MemoryThresholdUp { get; set; } = 80.0;
    public double MemoryThresholdDown { get; set; } = 40.0;
    public double RequestRateThresholdUp { get; set; } = 1000.0;
    public double RequestRateThresholdDown { get; set; } = 100.0;
}

public sealed class SystemMetrics
{
    public double CpuUtilization { get; set; }
    public double MemoryUtilization { get; set; }
    public int ActiveConnections { get; set; }
    public double RequestRate { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ScalingDecision
{
    public bool ShouldScale { get; set; }
    public int TargetReplicas { get; set; }
    public string Reason { get; set; } = string.Empty;
    public SystemMetrics? Metrics { get; set; }
}

public sealed class ScalingEvent
{
    public DateTime Timestamp { get; set; }
    public int FromReplicas { get; set; }
    public int ToReplicas { get; set; }
    public string Reason { get; set; } = string.Empty;
    public SystemMetrics? Metrics { get; set; }
}

public sealed class LoadMetrics
{
    public string Endpoint { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public double AverageResponseTime { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ScalingStatus
{
    public int CurrentReplicas { get; set; }
    public int MinReplicas { get; set; }
    public int MaxReplicas { get; set; }
    public DateTime LastScaleAction { get; set; }
    public IReadOnlyList<ScalingEvent> RecentEvents { get; init; } = [];
    public bool IsInCooldown { get; set; }
}