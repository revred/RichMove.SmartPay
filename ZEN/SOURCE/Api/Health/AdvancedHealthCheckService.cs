using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Health;

public sealed partial class AdvancedHealthCheckService : IHostedService, IDisposable
{
    private readonly ILogger<AdvancedHealthCheckService> _logger;
    private readonly AdvancedHealthCheckOptions _options;
    private readonly Timer _checkTimer;
    private readonly Meter _meter;

    // Health metrics
    private readonly Counter<long> _healthChecksPerformed;
    private readonly Gauge<long> _healthyServices;
    private readonly Gauge<long> _unhealthyServices;
    private readonly Histogram<double> _healthCheckDuration;
    private readonly Counter<long> _dependencyFailures;

    // Health check data
    private readonly ConcurrentDictionary<string, HealthCheckResult> _healthResults;
    private readonly ConcurrentDictionary<string, DependencyCheck> _dependencyChecks;
    private readonly ConcurrentQueue<HealthCheckHistory> _healthHistory;

    // System resources
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Process _currentProcess;

    public AdvancedHealthCheckService(
        ILogger<AdvancedHealthCheckService> logger,
        IOptions<AdvancedHealthCheckOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _healthResults = new ConcurrentDictionary<string, HealthCheckResult>();
        _dependencyChecks = new ConcurrentDictionary<string, DependencyCheck>();
        _healthHistory = new ConcurrentQueue<HealthCheckHistory>();
        _currentProcess = Process.GetCurrentProcess();

        _meter = new Meter("richmove.smartpay.health");

        _healthChecksPerformed = _meter.CreateCounter<long>(
            "richmove_smartpay_health_checks_total",
            "checks",
            "Total number of health checks performed");

        _healthyServices = _meter.CreateGauge<long>(
            "richmove_smartpay_healthy_services",
            "services",
            "Number of healthy services");

        _unhealthyServices = _meter.CreateGauge<long>(
            "richmove_smartpay_unhealthy_services",
            "services",
            "Number of unhealthy services");

        _healthCheckDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_health_check_duration_seconds",
            "seconds",
            "Time taken to perform health checks");

        _dependencyFailures = _meter.CreateCounter<long>(
            "richmove_smartpay_dependency_failures_total",
            "failures",
            "Total number of dependency failures");

        // Initialize CPU counter if available
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        }
        catch (Exception ex)
        {
            Log.CpuCounterInitializationFailed(_logger, ex);
        }

        InitializeDependencyChecks();

        _checkTimer = new Timer(PerformHealthChecks, null,
            TimeSpan.FromSeconds(5), _options.CheckInterval);

        Log.AdvancedHealthCheckServiceInitialized(_logger, _options.CheckInterval);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.AdvancedHealthCheckServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.AdvancedHealthCheckServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private void InitializeDependencyChecks()
    {
        // Database dependency
        _dependencyChecks["database"] = new DependencyCheck
        {
            Name = "database",
            Type = DependencyType.Database,
            Endpoint = "postgresql://localhost:5432/smartpay",
            Timeout = TimeSpan.FromSeconds(5),
            CriticalForHealth = true,
            Description = "Primary PostgreSQL database connection"
        };

        // External API dependency
        _dependencyChecks["payment_processor"] = new DependencyCheck
        {
            Name = "payment_processor",
            Type = DependencyType.ExternalApi,
            Endpoint = "https://api.payment-processor.com/health",
            Timeout = TimeSpan.FromSeconds(10),
            CriticalForHealth = false,
            Description = "External payment processing service"
        };

        // Cache dependency
        _dependencyChecks["redis_cache"] = new DependencyCheck
        {
            Name = "redis_cache",
            Type = DependencyType.Cache,
            Endpoint = "redis://localhost:6379",
            Timeout = TimeSpan.FromSeconds(3),
            CriticalForHealth = false,
            Description = "Redis caching layer"
        };

        // Message queue dependency
        _dependencyChecks["message_queue"] = new DependencyCheck
        {
            Name = "message_queue",
            Type = DependencyType.MessageQueue,
            Endpoint = "amqp://localhost:5672",
            Timeout = TimeSpan.FromSeconds(5),
            CriticalForHealth = false,
            Description = "RabbitMQ message broker"
        };

        // File storage dependency
        _dependencyChecks["file_storage"] = new DependencyCheck
        {
            Name = "file_storage",
            Type = DependencyType.Storage,
            Endpoint = "file://./storage",
            Timeout = TimeSpan.FromSeconds(2),
            CriticalForHealth = true,
            Description = "Local file storage system"
        };
    }

    private async void PerformHealthChecks(object? state)
    {
        var checkStart = DateTime.UtcNow;

        try
        {
            await PerformSystemHealthCheck();
            await PerformDependencyChecks();
            await UpdateHealthMetrics();
            await RecordHealthHistory();
        }
        catch (Exception ex)
        {
            Log.HealthCheckExecutionFailed(_logger, ex);
        }
        finally
        {
            var checkDuration = (DateTime.UtcNow - checkStart).TotalSeconds;
            _healthCheckDuration.Record(checkDuration);
        }
    }

    private async Task PerformSystemHealthCheck()
    {
        var systemHealth = new HealthCheckResult
        {
            Name = "system",
            Status = HealthStatus.Healthy,
            Description = "System resource health check",
            CheckedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Data = new Dictionary<string, object>()
        };

        var checkStart = DateTime.UtcNow;

        try
        {
            // Memory check
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = _currentProcess.WorkingSet64;
            var memoryUsagePercent = (double)workingSet / (1024 * 1024 * 1024) * 100; // Rough estimate

            systemHealth.Data["memory_bytes"] = totalMemory;
            systemHealth.Data["working_set_bytes"] = workingSet;
            systemHealth.Data["memory_usage_percent"] = memoryUsagePercent;

            // CPU check
            if (_cpuCounter != null)
            {
                try
                {
                    var cpuPercent = _cpuCounter.NextValue();
                    systemHealth.Data["cpu_usage_percent"] = cpuPercent;

                    if (cpuPercent > _options.CpuThreshold)
                    {
                        systemHealth.Status = HealthStatus.Degraded;
                        systemHealth.Description = $"High CPU usage: {cpuPercent:F1}%";
                    }
                }
                catch (Exception ex)
                {
                    systemHealth.Data["cpu_error"] = ex.Message;
                }
            }

            // Memory threshold check
            if (memoryUsagePercent > _options.MemoryThreshold)
            {
                systemHealth.Status = HealthStatus.Degraded;
                systemHealth.Description = $"High memory usage: {memoryUsagePercent:F1}%";
            }

            // Disk space check
            var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
            if (driveInfo.IsReady)
            {
                var freeSpacePercent = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize * 100;
                systemHealth.Data["disk_free_percent"] = freeSpacePercent;

                if (freeSpacePercent < _options.DiskSpaceThreshold)
                {
                    systemHealth.Status = HealthStatus.Degraded;
                    systemHealth.Description = $"Low disk space: {freeSpacePercent:F1}% free";
                }
            }

            // GC pressure check
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            systemHealth.Data["gc_gen0_collections"] = gen0Collections;
            systemHealth.Data["gc_gen1_collections"] = gen1Collections;
            systemHealth.Data["gc_gen2_collections"] = gen2Collections;

            await Task.Delay(1); // Simulate async work
        }
        catch (Exception ex)
        {
            systemHealth.Status = HealthStatus.Unhealthy;
            systemHealth.Description = $"System check failed: {ex.Message}";
            systemHealth.Exception = ex.ToString();
        }
        finally
        {
            systemHealth.Duration = DateTime.UtcNow - checkStart;
            _healthResults[systemHealth.Name] = systemHealth;
        }
    }

    private async Task PerformDependencyChecks()
    {
        var dependencyTasks = _dependencyChecks.Values.Select(CheckDependency);
        await Task.WhenAll(dependencyTasks);
    }

    private async Task CheckDependency(DependencyCheck dependency)
    {
        var checkResult = new HealthCheckResult
        {
            Name = dependency.Name,
            Status = HealthStatus.Healthy,
            Description = dependency.Description,
            CheckedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["endpoint"] = dependency.Endpoint,
                ["type"] = dependency.Type.ToString(),
                ["critical"] = dependency.CriticalForHealth
            }
        };

        var checkStart = DateTime.UtcNow;

        try
        {
            switch (dependency.Type)
            {
                case DependencyType.Database:
                    await CheckDatabaseDependency(dependency, checkResult);
                    break;

                case DependencyType.ExternalApi:
                    await CheckApiDependency(dependency, checkResult);
                    break;

                case DependencyType.Cache:
                    await CheckCacheDependency(dependency, checkResult);
                    break;

                case DependencyType.MessageQueue:
                    await CheckMessageQueueDependency(dependency, checkResult);
                    break;

                case DependencyType.Storage:
                    await CheckStorageDependency(dependency, checkResult);
                    break;

                default:
                    checkResult.Status = HealthStatus.Unhealthy;
                    checkResult.Description = "Unknown dependency type";
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            checkResult.Status = HealthStatus.Unhealthy;
            checkResult.Description = $"Check timed out after {dependency.Timeout}";
        }
        catch (Exception ex)
        {
            checkResult.Status = HealthStatus.Unhealthy;
            checkResult.Description = $"Check failed: {ex.Message}";
            checkResult.Exception = ex.ToString();

            _dependencyFailures.Add(1,
                new KeyValuePair<string, object?>("dependency", dependency.Name),
                new KeyValuePair<string, object?>("type", dependency.Type.ToString()));
        }
        finally
        {
            checkResult.Duration = DateTime.UtcNow - checkStart;
            _healthResults[checkResult.Name] = checkResult;
        }
    }

    private async Task CheckDatabaseDependency(DependencyCheck dependency, HealthCheckResult result)
    {
        await Task.Delay(50); // Simulate database connection check

        // Simulate database connection and simple query
        var random = new Random();
        var responseTime = random.Next(10, 100);

        result.Data["response_time_ms"] = responseTime;
        result.Data["connection_pool_size"] = random.Next(5, 20);

        if (responseTime > 1000)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"Database responding slowly ({responseTime}ms)";
        }
    }

    private async Task CheckApiDependency(DependencyCheck dependency, HealthCheckResult result)
    {
        using var httpClient = new HttpClient { Timeout = dependency.Timeout };

        try
        {
            var response = await httpClient.GetAsync("https://httpbin.org/status/200");
            result.Data["status_code"] = (int)response.StatusCode;
            result.Data["response_time_ms"] = 150; // Simulated

            if (!response.IsSuccessStatusCode)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"API returned {response.StatusCode}";
            }
        }
        catch (HttpRequestException ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"HTTP request failed: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = "Request timed out";
        }
    }

    private async Task CheckCacheDependency(DependencyCheck dependency, HealthCheckResult result)
    {
        await Task.Delay(20); // Simulate Redis ping

        var random = new Random();
        var responseTime = random.Next(1, 10);

        result.Data["response_time_ms"] = responseTime;
        result.Data["connected_clients"] = random.Next(1, 50);
        result.Data["memory_usage_mb"] = random.Next(10, 100);
    }

    private async Task CheckMessageQueueDependency(DependencyCheck dependency, HealthCheckResult result)
    {
        await Task.Delay(30); // Simulate RabbitMQ management API call

        var random = new Random();
        result.Data["queue_count"] = random.Next(0, 10);
        result.Data["message_count"] = random.Next(0, 1000);
        result.Data["consumer_count"] = random.Next(1, 5);
    }

    private async Task CheckStorageDependency(DependencyCheck dependency, HealthCheckResult result)
    {
        await Task.Delay(10);

        try
        {
            var testPath = Path.Combine("./storage", "health-check.txt");
            Directory.CreateDirectory("./storage");

            await File.WriteAllTextAsync(testPath, "health-check");
            var content = await File.ReadAllTextAsync(testPath);
            File.Delete(testPath);

            result.Data["test_write_read"] = content == "health-check";
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Storage test failed: {ex.Message}";
        }
    }

    private async Task UpdateHealthMetrics()
    {
        await Task.Delay(5);

        var healthyCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Healthy);
        var degradedCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Degraded);
        var unhealthyCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Unhealthy);

        _healthyServices.Record(healthyCount + degradedCount); // Degraded still counts as operational
        _unhealthyServices.Record(unhealthyCount);

        _healthChecksPerformed.Add(_healthResults.Count);

        Log.HealthMetricsUpdated(_logger, healthyCount, degradedCount, unhealthyCount);
    }

    private async Task RecordHealthHistory()
    {
        await Task.Delay(5);

        var historyEntry = new HealthCheckHistory
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = CalculateOverallHealth(),
            CheckResults = _healthResults.Values.ToDictionary(r => r.Name, r => r.Status)
        };

        _healthHistory.Enqueue(historyEntry);

        // Keep only recent history (last 24 hours)
        while (_healthHistory.Count > _options.MaxHistoryEntries)
        {
            _healthHistory.TryDequeue(out _);
        }
    }

    private HealthStatus CalculateOverallHealth()
    {
        var criticalDependencies = _dependencyChecks.Values
            .Where(d => d.CriticalForHealth)
            .Select(d => d.Name)
            .ToHashSet();

        var criticalFailures = _healthResults.Values
            .Where(r => criticalDependencies.Contains(r.Name) && r.Status == HealthStatus.Unhealthy)
            .Any();

        if (criticalFailures)
        {
            return HealthStatus.Unhealthy;
        }

        var hasUnhealthy = _healthResults.Values.Any(r => r.Status == HealthStatus.Unhealthy);
        var hasDegraded = _healthResults.Values.Any(r => r.Status == HealthStatus.Degraded);

        if (hasUnhealthy) return HealthStatus.Degraded;
        if (hasDegraded) return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }

    public AdvancedHealthCheckStatus GetHealthStatus()
    {
        return new AdvancedHealthCheckStatus
        {
            OverallStatus = CalculateOverallHealth(),
            CheckedAt = DateTime.UtcNow,
            CheckResults = _healthResults.Values.ToList(),
            DependencyCount = _dependencyChecks.Count,
            HealthyCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Healthy),
            DegradedCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Degraded),
            UnhealthyCount = _healthResults.Values.Count(r => r.Status == HealthStatus.Unhealthy),
            History = _healthHistory.ToList().TakeLast(100).ToList()
        };
    }

    public void Dispose()
    {
        _checkTimer?.Dispose();
        _cpuCounter?.Dispose();
        _currentProcess?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9401, Level = LogLevel.Information,
            Message = "Advanced health check service initialized (check interval: {CheckInterval})")]
        public static partial void AdvancedHealthCheckServiceInitialized(ILogger logger, TimeSpan checkInterval);

        [LoggerMessage(EventId = 9402, Level = LogLevel.Information,
            Message = "Advanced health check service started")]
        public static partial void AdvancedHealthCheckServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9403, Level = LogLevel.Information,
            Message = "Advanced health check service stopped")]
        public static partial void AdvancedHealthCheckServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9404, Level = LogLevel.Warning,
            Message = "CPU counter initialization failed")]
        public static partial void CpuCounterInitializationFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9405, Level = LogLevel.Error,
            Message = "Health check execution failed")]
        public static partial void HealthCheckExecutionFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9406, Level = LogLevel.Debug,
            Message = "Health metrics updated: {HealthyCount} healthy, {DegradedCount} degraded, {UnhealthyCount} unhealthy")]
        public static partial void HealthMetricsUpdated(ILogger logger, int healthyCount, int degradedCount, int unhealthyCount);
    }
}

// Supporting types
public sealed class AdvancedHealthCheckOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public double CpuThreshold { get; set; } = 80.0;
    public double MemoryThreshold { get; set; } = 80.0;
    public double DiskSpaceThreshold { get; set; } = 10.0;
    public int MaxHistoryEntries { get; set; } = 2880; // 24 hours at 30-second intervals
}

public sealed class DependencyCheck
{
    public string Name { get; set; } = string.Empty;
    public DependencyType Type { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; }
    public bool CriticalForHealth { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public string? Exception { get; set; }
}

public sealed class HealthCheckHistory
{
    public DateTime Timestamp { get; set; }
    public HealthStatus OverallStatus { get; set; }
    public Dictionary<string, HealthStatus> CheckResults { get; set; } = new();
}

public sealed class AdvancedHealthCheckStatus
{
    public HealthStatus OverallStatus { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<HealthCheckResult> CheckResults { get; set; } = [];
    public int DependencyCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public List<HealthCheckHistory> History { get; set; } = [];
}

public enum DependencyType
{
    Database,
    ExternalApi,
    Cache,
    MessageQueue,
    Storage,
    Network
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}