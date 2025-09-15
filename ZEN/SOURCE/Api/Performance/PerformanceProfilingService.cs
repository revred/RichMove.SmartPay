using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RichMove.SmartPay.Api.Performance;

public sealed partial class PerformanceProfilingService : IHostedService, IDisposable
{
    private readonly ILogger<PerformanceProfilingService> _logger;
    private readonly PerformanceProfilingOptions _options;
    private readonly Timer _profilingTimer;
    private readonly Timer _analysisTimer;
    private readonly Meter _meter;

    // Performance metrics
    private readonly Counter<long> _profileSamples;
    private readonly Counter<long> _bottlenecksDetected;
    private readonly Histogram<double> _methodDuration;
    private readonly Gauge<long> _activeProfilers;

    // Profiling data
    private readonly ConcurrentDictionary<string, MethodProfile> _methodProfiles;
    private readonly ConcurrentDictionary<string, HotPath> _hotPaths;
    private readonly ConcurrentQueue<PerformanceSample> _samples;
    private readonly ConcurrentQueue<Bottleneck> _detectedBottlenecks;

    // System monitoring
    private readonly Process _currentProcess;
    private readonly ConcurrentDictionary<string, ResourceUsage> _resourceSnapshots;

    // Profiling state
    private readonly Dictionary<string, Stopwatch> _activeProfiles;
    private readonly Random _random = new();

    public PerformanceProfilingService(
        ILogger<PerformanceProfilingService> logger,
        IOptions<PerformanceProfilingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _methodProfiles = new ConcurrentDictionary<string, MethodProfile>();
        _hotPaths = new ConcurrentDictionary<string, HotPath>();
        _samples = new ConcurrentQueue<PerformanceSample>();
        _detectedBottlenecks = new ConcurrentQueue<Bottleneck>();
        _resourceSnapshots = new ConcurrentDictionary<string, ResourceUsage>();
        _activeProfiles = new Dictionary<string, Stopwatch>();
        _currentProcess = Process.GetCurrentProcess();

        _meter = new Meter("richmove.smartpay.profiling");

        _profileSamples = _meter.CreateCounter<long>(
            "richmove_smartpay_profile_samples_total",
            "samples",
            "Total number of profile samples collected");

        _bottlenecksDetected = _meter.CreateCounter<long>(
            "richmove_smartpay_bottlenecks_detected_total",
            "bottlenecks",
            "Total number of performance bottlenecks detected");

        _methodDuration = _meter.CreateHistogram<double>(
            "richmove_smartpay_method_duration_seconds",
            "seconds",
            "Method execution duration in seconds");

        _activeProfilers = _meter.CreateGauge<long>(
            "richmove_smartpay_active_profilers",
            "profilers",
            "Number of active profilers");

        _profilingTimer = new Timer(CollectProfilingSamples, null,
            TimeSpan.FromSeconds(1), _options.SamplingInterval);

        _analysisTimer = new Timer(AnalyzePerformanceData, null,
            TimeSpan.FromSeconds(30), _options.AnalysisInterval);

        Log.PerformanceProfilingServiceInitialized(_logger, _options.SamplingInterval, _options.AnalysisInterval);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.PerformanceProfilingServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.PerformanceProfilingServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private async void CollectProfilingSamples(object? state)
    {
        try
        {
            await CollectSystemResourceSample();
            await CollectGarbageCollectionSample();
            await CollectThreadPoolSample();
        }
        catch (Exception ex)
        {
            Log.ProfilingSampleCollectionFailed(_logger, ex);
        }
    }

    private async void AnalyzePerformanceData(object? state)
    {
        try
        {
            await AnalyzeMethodProfiles();
            await DetectBottlenecks();
            await UpdateHotPaths();
            await GeneratePerformanceInsights();
        }
        catch (Exception ex)
        {
            Log.PerformanceAnalysisFailed(_logger, ex);
        }
    }

    private async Task CollectSystemResourceSample()
    {
        await Task.Delay(5);

        var sample = new PerformanceSample
        {
            Timestamp = DateTime.UtcNow,
            Type = SampleType.SystemResource,
            Data = new Dictionary<string, object>()
        };

        try
        {
            // Memory metrics
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;

            sample.Data["total_memory_bytes"] = totalMemory;
            sample.Data["working_set_bytes"] = workingSet;
            sample.Data["private_memory_bytes"] = privateMemory;

            // CPU time
            var totalCpuTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
            var userCpuTime = _currentProcess.UserProcessorTime.TotalMilliseconds;

            sample.Data["total_cpu_time_ms"] = totalCpuTime;
            sample.Data["user_cpu_time_ms"] = userCpuTime;

            // Handle count
            sample.Data["handle_count"] = _currentProcess.HandleCount;

            _samples.Enqueue(sample);
            _profileSamples.Add(1, new KeyValuePair<string, object?>("type", "system"));
        }
        catch (Exception ex)
        {
            Log.SystemResourceSampleFailed(_logger, ex);
        }
    }

    private async Task CollectGarbageCollectionSample()
    {
        await Task.Delay(2);

        var sample = new PerformanceSample
        {
            Timestamp = DateTime.UtcNow,
            Type = SampleType.GarbageCollection,
            Data = new Dictionary<string, object>
            {
                ["gen0_collections"] = GC.CollectionCount(0),
                ["gen1_collections"] = GC.CollectionCount(1),
                ["gen2_collections"] = GC.CollectionCount(2),
                ["total_memory"] = GC.GetTotalMemory(false),
                ["allocated_bytes"] = GC.GetTotalAllocatedBytes(false)
            }
        };

        _samples.Enqueue(sample);
        _profileSamples.Add(1, new KeyValuePair<string, object?>("type", "gc"));
    }

    private async Task CollectThreadPoolSample()
    {
        await Task.Delay(1);

        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

        var sample = new PerformanceSample
        {
            Timestamp = DateTime.UtcNow,
            Type = SampleType.ThreadPool,
            Data = new Dictionary<string, object>
            {
                ["available_worker_threads"] = availableWorkerThreads,
                ["available_completion_port_threads"] = availableCompletionPortThreads,
                ["max_worker_threads"] = maxWorkerThreads,
                ["max_completion_port_threads"] = maxCompletionPortThreads,
                ["busy_worker_threads"] = maxWorkerThreads - availableWorkerThreads,
                ["busy_completion_port_threads"] = maxCompletionPortThreads - availableCompletionPortThreads
            }
        };

        _samples.Enqueue(sample);
        _profileSamples.Add(1, new KeyValuePair<string, object?>("type", "threadpool"));
    }

    private async Task AnalyzeMethodProfiles()
    {
        await Task.Delay(15);

        var slowMethods = _methodProfiles.Values
            .Where(p => p.CallCount > _options.MinCallCountForAnalysis)
            .Where(p => (p.TotalDuration.TotalMilliseconds / p.CallCount) > _options.SlowMethodThresholdMs)
            .OrderByDescending(p => p.TotalDuration.TotalMilliseconds)
            .Take(10)
            .ToList();

        if (slowMethods.Count > 0)
        {
            Log.SlowMethodsDetected(_logger, slowMethods.Count);

            foreach (var method in slowMethods)
            {
                var avgDuration = method.TotalDuration.TotalMilliseconds / method.CallCount;
                Log.SlowMethodDetails(_logger, method.MethodName, method.CallCount, avgDuration);
            }
        }
    }

    private async Task DetectBottlenecks()
    {
        await Task.Delay(20);

        var recentSamples = _samples.Where(s => s.Timestamp > DateTime.UtcNow.AddMinutes(-5)).ToList();

        await DetectMemoryBottlenecks(recentSamples);
        await DetectCpuBottlenecks(recentSamples);
        await DetectThreadPoolBottlenecks(recentSamples);
        await DetectGarbageCollectionBottlenecks(recentSamples);
    }

    private async Task DetectMemoryBottlenecks(List<PerformanceSample> samples)
    {
        await Task.Delay(5);

        var memorySamples = samples.Where(s => s.Type == SampleType.SystemResource).ToList();
        if (memorySamples.Count < 2) return;

        var memoryUsages = memorySamples
            .Select(s => (long)s.Data["working_set_bytes"])
            .ToList();

        var avgMemoryUsage = memoryUsages.Average();
        var maxMemoryUsage = memoryUsages.Max();
        var memoryGrowthRate = (memoryUsages.Last() - memoryUsages.First()) / (double)memoryUsages.First() * 100;

        if (avgMemoryUsage > _options.MemoryBottleneckThresholdBytes)
        {
            var bottleneck = new Bottleneck
            {
                Id = Guid.NewGuid().ToString(),
                Type = BottleneckType.Memory,
                Description = $"High average memory usage: {avgMemoryUsage / 1024 / 1024:F1} MB",
                Severity = maxMemoryUsage > _options.MemoryBottleneckThresholdBytes * 1.5 ?
                    BottleneckSeverity.Critical : BottleneckSeverity.Warning,
                DetectedAt = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    ["avg_memory_mb"] = avgMemoryUsage / 1024 / 1024,
                    ["max_memory_mb"] = maxMemoryUsage / 1024 / 1024,
                    ["growth_rate_percent"] = memoryGrowthRate
                }
            };

            _detectedBottlenecks.Enqueue(bottleneck);
            _bottlenecksDetected.Add(1, new KeyValuePair<string, object?>("type", "memory"));

            Log.BottleneckDetected(_logger, bottleneck.Type.ToString(), bottleneck.Severity.ToString(), bottleneck.Description);
        }
    }

    private async Task DetectCpuBottlenecks(List<PerformanceSample> samples)
    {
        await Task.Delay(3);

        // CPU bottleneck detection would require CPU usage calculation over time
        // This is a simplified version focusing on method profiling
        var highCpuMethods = _methodProfiles.Values
            .Where(p => p.CallCount > 100)
            .Where(p => p.TotalDuration.TotalMilliseconds > 1000)
            .OrderByDescending(p => p.TotalDuration.TotalMilliseconds)
            .Take(5)
            .ToList();

        if (highCpuMethods.Count > 0)
        {
            var bottleneck = new Bottleneck
            {
                Id = Guid.NewGuid().ToString(),
                Type = BottleneckType.CPU,
                Description = $"High CPU usage detected in {highCpuMethods.Count} methods",
                Severity = BottleneckSeverity.Warning,
                DetectedAt = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    ["high_cpu_method_count"] = highCpuMethods.Count,
                    ["top_method"] = highCpuMethods.First().MethodName,
                    ["top_method_total_ms"] = highCpuMethods.First().TotalDuration.TotalMilliseconds
                }
            };

            _detectedBottlenecks.Enqueue(bottleneck);
            _bottlenecksDetected.Add(1, new KeyValuePair<string, object?>("type", "cpu"));

            Log.BottleneckDetected(_logger, bottleneck.Type.ToString(), bottleneck.Severity.ToString(), bottleneck.Description);
        }
    }

    private async Task DetectThreadPoolBottlenecks(List<PerformanceSample> samples)
    {
        await Task.Delay(3);

        var threadPoolSamples = samples.Where(s => s.Type == SampleType.ThreadPool).ToList();
        if (threadPoolSamples.Count == 0) return;

        var recentSample = threadPoolSamples.Last();
        var busyWorkerThreads = (int)recentSample.Data["busy_worker_threads"];
        var maxWorkerThreads = (int)recentSample.Data["max_worker_threads"];

        var threadPoolUtilization = (double)busyWorkerThreads / maxWorkerThreads * 100;

        if (threadPoolUtilization > _options.ThreadPoolBottleneckThresholdPercent)
        {
            var bottleneck = new Bottleneck
            {
                Id = Guid.NewGuid().ToString(),
                Type = BottleneckType.ThreadPool,
                Description = $"High thread pool utilization: {threadPoolUtilization:F1}%",
                Severity = threadPoolUtilization > 90 ? BottleneckSeverity.Critical : BottleneckSeverity.Warning,
                DetectedAt = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    ["utilization_percent"] = threadPoolUtilization,
                    ["busy_threads"] = busyWorkerThreads,
                    ["max_threads"] = maxWorkerThreads
                }
            };

            _detectedBottlenecks.Enqueue(bottleneck);
            _bottlenecksDetected.Add(1, new KeyValuePair<string, object?>("type", "threadpool"));

            Log.BottleneckDetected(_logger, bottleneck.Type.ToString(), bottleneck.Severity.ToString(), bottleneck.Description);
        }
    }

    private async Task DetectGarbageCollectionBottlenecks(List<PerformanceSample> samples)
    {
        await Task.Delay(3);

        var gcSamples = samples.Where(s => s.Type == SampleType.GarbageCollection).ToList();
        if (gcSamples.Count < 2) return;

        var recentGcSample = gcSamples.Last();
        var previousGcSample = gcSamples[gcSamples.Count - 2];

        var gen2CollectionIncrease = (int)recentGcSample.Data["gen2_collections"] -
                                   (int)previousGcSample.Data["gen2_collections"];

        if (gen2CollectionIncrease > _options.GcBottleneckThreshold)
        {
            var bottleneck = new Bottleneck
            {
                Id = Guid.NewGuid().ToString(),
                Type = BottleneckType.GarbageCollection,
                Description = $"Frequent Gen2 garbage collections: {gen2CollectionIncrease} in recent interval",
                Severity = BottleneckSeverity.Warning,
                DetectedAt = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    ["gen2_collection_increase"] = gen2CollectionIncrease,
                    ["total_memory_mb"] = (long)recentGcSample.Data["total_memory"] / 1024 / 1024
                }
            };

            _detectedBottlenecks.Enqueue(bottleneck);
            _bottlenecksDetected.Add(1, new KeyValuePair<string, object?>("type", "gc"));

            Log.BottleneckDetected(_logger, bottleneck.Type.ToString(), bottleneck.Severity.ToString(), bottleneck.Description);
        }
    }

    private async Task UpdateHotPaths()
    {
        await Task.Delay(10);

        var frequentMethods = _methodProfiles.Values
            .Where(p => p.CallCount > _options.MinCallCountForHotPath)
            .OrderByDescending(p => p.CallCount)
            .Take(20)
            .ToList();

        foreach (var method in frequentMethods)
        {
            var hotPathKey = method.ClassName ?? "Unknown";
            _hotPaths.AddOrUpdate(hotPathKey,
                new HotPath
                {
                    PathName = hotPathKey,
                    Methods = [method],
                    TotalCalls = method.CallCount,
                    TotalDuration = method.TotalDuration,
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    if (!existing.Methods.Any(m => m.MethodName == method.MethodName))
                    {
                        existing.Methods.Add(method);
                        existing.TotalCalls += method.CallCount;
                        existing.TotalDuration = existing.TotalDuration.Add(method.TotalDuration);
                        existing.LastUpdated = DateTime.UtcNow;
                    }
                    return existing;
                });
        }

        Log.HotPathsUpdated(_logger, _hotPaths.Count);
    }

    private async Task GeneratePerformanceInsights()
    {
        await Task.Delay(5);

        var insights = new List<string>();

        // Memory insights
        var recentMemorySamples = _samples
            .Where(s => s.Type == SampleType.SystemResource && s.Timestamp > DateTime.UtcNow.AddMinutes(-5))
            .ToList();

        if (recentMemorySamples.Count > 0)
        {
            var avgMemory = recentMemorySamples.Average(s => (long)s.Data["working_set_bytes"]) / 1024 / 1024;
            insights.Add($"Average memory usage: {avgMemory:F1} MB");
        }

        // Method performance insights
        var topMethod = _methodProfiles.Values
            .OrderByDescending(p => p.TotalDuration.TotalMilliseconds)
            .FirstOrDefault();

        if (topMethod != null)
        {
            insights.Add($"Most time-consuming method: {topMethod.MethodName} ({topMethod.TotalDuration.TotalMilliseconds:F1}ms total)");
        }

        Log.PerformanceInsightsGenerated(_logger, insights.Count);
    }

    public string StartMethodProfiling(string methodName, string? className = null)
    {
        var profileId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        lock (_activeProfiles)
        {
            _activeProfiles[profileId] = stopwatch;
        }

        _activeProfilers.Record(_activeProfiles.Count);

        Log.MethodProfilingStarted(_logger, methodName, profileId);
        return profileId;
    }

    public void EndMethodProfiling(string profileId, string methodName, string? className = null)
    {
        Stopwatch? stopwatch = null;

        lock (_activeProfiles)
        {
            if (_activeProfiles.TryGetValue(profileId, out stopwatch))
            {
                _activeProfiles.Remove(profileId);
            }
        }

        if (stopwatch != null)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            var methodKey = $"{className}.{methodName}";
            _methodProfiles.AddOrUpdate(methodKey,
                new MethodProfile
                {
                    MethodName = methodName,
                    ClassName = className,
                    CallCount = 1,
                    TotalDuration = duration,
                    MinDuration = duration,
                    MaxDuration = duration,
                    LastCalled = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.CallCount++;
                    existing.TotalDuration = existing.TotalDuration.Add(duration);
                    if (duration < existing.MinDuration) existing.MinDuration = duration;
                    if (duration > existing.MaxDuration) existing.MaxDuration = duration;
                    existing.LastCalled = DateTime.UtcNow;
                    return existing;
                });

            _methodDuration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("method", methodName),
                new KeyValuePair<string, object?>("class", className ?? "Unknown"));

            _activeProfilers.Record(_activeProfiles.Count);

            Log.MethodProfilingCompleted(_logger, methodName, profileId, duration.TotalMilliseconds);
        }
    }

    public PerformanceProfilingStatus GetProfilingStatus()
    {
        return new PerformanceProfilingStatus
        {
            IsActive = _options.IsEnabled,
            ActiveProfilers = _activeProfiles.Count,
            TotalSamples = _samples.Count,
            MethodProfiles = _methodProfiles.Count,
            HotPaths = _hotPaths.Count,
            DetectedBottlenecks = _detectedBottlenecks.Count,
            TopMethods = _methodProfiles.Values
                .OrderByDescending(p => p.TotalDuration.TotalMilliseconds)
                .Take(10)
                .Select(p => new MethodPerformanceSummary
                {
                    MethodName = p.MethodName,
                    ClassName = p.ClassName,
                    CallCount = p.CallCount,
                    TotalDurationMs = p.TotalDuration.TotalMilliseconds,
                    AvgDurationMs = p.TotalDuration.TotalMilliseconds / p.CallCount,
                    MaxDurationMs = p.MaxDuration.TotalMilliseconds
                })
                .ToList(),
            RecentBottlenecks = _detectedBottlenecks.ToList().TakeLast(10).ToList()
        };
    }

    public void Dispose()
    {
        _profilingTimer?.Dispose();
        _analysisTimer?.Dispose();
        _currentProcess?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9501, Level = LogLevel.Information,
            Message = "Performance profiling service initialized (sampling: {SamplingInterval}, analysis: {AnalysisInterval})")]
        public static partial void PerformanceProfilingServiceInitialized(ILogger logger, TimeSpan samplingInterval, TimeSpan analysisInterval);

        [LoggerMessage(EventId = 9502, Level = LogLevel.Information,
            Message = "Performance profiling service started")]
        public static partial void PerformanceProfilingServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9503, Level = LogLevel.Information,
            Message = "Performance profiling service stopped")]
        public static partial void PerformanceProfilingServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9504, Level = LogLevel.Error,
            Message = "Profiling sample collection failed")]
        public static partial void ProfilingSampleCollectionFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9505, Level = LogLevel.Error,
            Message = "Performance analysis failed")]
        public static partial void PerformanceAnalysisFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9506, Level = LogLevel.Error,
            Message = "System resource sample collection failed")]
        public static partial void SystemResourceSampleFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9507, Level = LogLevel.Warning,
            Message = "Detected {SlowMethodCount} slow methods")]
        public static partial void SlowMethodsDetected(ILogger logger, int slowMethodCount);

        [LoggerMessage(EventId = 9508, Level = LogLevel.Debug,
            Message = "Slow method: {MethodName} called {CallCount} times, avg {AvgDurationMs:F2}ms")]
        public static partial void SlowMethodDetails(ILogger logger, string methodName, long callCount, double avgDurationMs);

        [LoggerMessage(EventId = 9509, Level = LogLevel.Warning,
            Message = "Performance bottleneck detected: {BottleneckType} ({Severity}) - {Description}")]
        public static partial void BottleneckDetected(ILogger logger, string bottleneckType, string severity, string description);

        [LoggerMessage(EventId = 9510, Level = LogLevel.Debug,
            Message = "Updated hot paths: {HotPathCount} total")]
        public static partial void HotPathsUpdated(ILogger logger, int hotPathCount);

        [LoggerMessage(EventId = 9511, Level = LogLevel.Debug,
            Message = "Generated {InsightCount} performance insights")]
        public static partial void PerformanceInsightsGenerated(ILogger logger, int insightCount);

        [LoggerMessage(EventId = 9512, Level = LogLevel.Debug,
            Message = "Started profiling method {MethodName} (profile ID: {ProfileId})")]
        public static partial void MethodProfilingStarted(ILogger logger, string methodName, string profileId);

        [LoggerMessage(EventId = 9513, Level = LogLevel.Debug,
            Message = "Completed profiling method {MethodName} (profile ID: {ProfileId}) in {DurationMs:F2}ms")]
        public static partial void MethodProfilingCompleted(ILogger logger, string methodName, string profileId, double durationMs);
    }
}

// Supporting types
public sealed class PerformanceProfilingOptions
{
    public bool IsEnabled { get; set; } = true;
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan AnalysisInterval { get; set; } = TimeSpan.FromMinutes(1);
    public double SlowMethodThresholdMs { get; set; } = 100.0;
    public int MinCallCountForAnalysis { get; set; } = 10;
    public int MinCallCountForHotPath { get; set; } = 50;
    public long MemoryBottleneckThresholdBytes { get; set; } = 500 * 1024 * 1024; // 500 MB
    public double ThreadPoolBottleneckThresholdPercent { get; set; } = 80.0;
    public int GcBottleneckThreshold { get; set; } = 3;
}

public sealed class PerformanceSample
{
    public DateTime Timestamp { get; set; }
    public SampleType Type { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public sealed class MethodProfile
{
    public string MethodName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public long CallCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public DateTime LastCalled { get; set; }
}

public sealed class HotPath
{
    public string PathName { get; set; } = string.Empty;
    public List<MethodProfile> Methods { get; set; } = [];
    public long TotalCalls { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public DateTime LastUpdated { get; set; }
}

public sealed class Bottleneck
{
    public string Id { get; set; } = string.Empty;
    public BottleneckType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public BottleneckSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public sealed class ResourceUsage
{
    public DateTime Timestamp { get; set; }
    public long MemoryBytes { get; set; }
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

public sealed class PerformanceProfilingStatus
{
    public bool IsActive { get; set; }
    public int ActiveProfilers { get; set; }
    public int TotalSamples { get; set; }
    public int MethodProfiles { get; set; }
    public int HotPaths { get; set; }
    public int DetectedBottlenecks { get; set; }
    public List<MethodPerformanceSummary> TopMethods { get; set; } = [];
    public List<Bottleneck> RecentBottlenecks { get; set; } = [];
}

public sealed class MethodPerformanceSummary
{
    public string MethodName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public long CallCount { get; set; }
    public double TotalDurationMs { get; set; }
    public double AvgDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
}

public enum SampleType
{
    SystemResource,
    GarbageCollection,
    ThreadPool,
    MethodExecution
}

public enum BottleneckType
{
    Memory,
    CPU,
    ThreadPool,
    GarbageCollection,
    IO,
    Network
}

public enum BottleneckSeverity
{
    Info,
    Warning,
    Critical
}