using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api.Idempotency;
using RichMove.SmartPay.Api.Monitoring;
using RichMove.SmartPay.Api.Performance;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Services;

/// <summary>
/// Background service for cleanup tasks and maintenance operations
/// Runs scheduled cleanup for idempotency store, metrics, and memory pools
/// </summary>
public sealed partial class CleanupBackgroundService : BackgroundService
{
    private readonly ILogger<CleanupBackgroundService> _logger;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;
    private readonly CleanupOptions _options;
    private readonly PeriodicTimer _cleanupTimer;

    public CleanupBackgroundService(
        ILogger<CleanupBackgroundService> logger,
        IServiceProvider services,
        IClock clock,
        IOptions<CleanupOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _services = services;
        _clock = clock;
        _options = options.Value;
        _cleanupTimer = new PeriodicTimer(_options.CleanupInterval);

        Log.BackgroundServiceInitialized(_logger, _options.CleanupInterval.TotalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.BackgroundServiceStarted(_logger);

        try
        {
            // Initial delay before first cleanup
            await Task.Delay(_options.InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupCycle(stoppingToken);
                    await _cleanupTimer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.CleanupCycleError(_logger, ex);

                    // Wait before retrying after error
                    try
                    {
                        await Task.Delay(_options.ErrorRetryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        finally
        {
            Log.BackgroundServiceStopped(_logger);
        }
    }

    private async Task PerformCleanupCycle(CancellationToken cancellationToken)
    {
        var startTime = _clock.UtcNow;
        var tasksCompleted = 0;
        var tasksSkipped = 0;

        Log.CleanupCycleStarted(_logger);

        // Task 1: Clean up idempotency store
        if (_options.EnableIdempotencyCleanup)
        {
            try
            {
                await CleanupIdempotencyStore(cancellationToken);
                tasksCompleted++;
            }
            catch (Exception ex)
            {
                Log.IdempotencyCleanupFailed(_logger, ex);
                tasksSkipped++;
            }
        }
        else
        {
            tasksSkipped++;
        }

        // Task 2: Clean up rate limit counters
        if (_options.EnableRateLimitCleanup)
        {
            try
            {
                await CleanupRateLimitCounters(cancellationToken);
                tasksCompleted++;
            }
            catch (Exception ex)
            {
                Log.RateLimitCleanupFailed(_logger, ex);
                tasksSkipped++;
            }
        }
        else
        {
            tasksSkipped++;
        }

        // Task 3: Cleanup memory pools
        if (_options.EnableMemoryPoolCleanup)
        {
            try
            {
                await CleanupMemoryPools(cancellationToken);
                tasksCompleted++;
            }
            catch (Exception ex)
            {
                Log.MemoryPoolCleanupFailed(_logger, ex);
                tasksSkipped++;
            }
        }
        else
        {
            tasksSkipped++;
        }

        // Task 4: Cleanup cold start tracker
        if (_options.EnableColdStartCleanup)
        {
            try
            {
                await CleanupColdStartTracker(cancellationToken);
                tasksCompleted++;
            }
            catch (Exception ex)
            {
                Log.ColdStartCleanupFailed(_logger, ex);
                tasksSkipped++;
            }
        }
        else
        {
            tasksSkipped++;
        }

        // Task 5: Force garbage collection if enabled
        if (_options.EnableGarbageCollection && tasksCompleted > 0)
        {
            try
            {
                await ForceGarbageCollection(cancellationToken);
                tasksCompleted++;
            }
            catch (Exception ex)
            {
                Log.GarbageCollectionFailed(_logger, ex);
                tasksSkipped++;
            }
        }

        var duration = _clock.UtcNow - startTime;
        Log.CleanupCycleCompleted(_logger, tasksCompleted, tasksSkipped, duration.TotalMilliseconds);
    }

    private async Task CleanupIdempotencyStore(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var idempotencyStore = scope.ServiceProvider.GetService<IIdempotencyStore>();

        if (idempotencyStore is InMemoryIdempotencyStore inMemoryStore)
        {
            var cleanedCount = await inMemoryStore.CleanupExpiredAsync(_options.IdempotencyRetentionPeriod, cancellationToken);
            Log.IdempotencyStoreCleanup(_logger, cleanedCount);
        }
    }

    private async Task CleanupRateLimitCounters(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var rateLimitCounters = scope.ServiceProvider.GetService<RateLimitCounters>();

        if (rateLimitCounters != null)
        {
            // Rate limit counters are already self-cleaning via time windows
            // This is a placeholder for future enhancements
            await Task.CompletedTask;
            Log.RateLimitCountersCleanup(_logger);
        }
    }

    private async Task CleanupMemoryPools(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var memoryPoolManager = scope.ServiceProvider.GetService<MemoryPoolManager>();

        if (memoryPoolManager != null)
        {
            var stats = memoryPoolManager.Statistics;

            // Log statistics for monitoring
            Log.MemoryPoolStats(_logger, stats.OutstandingByteArrays, stats.OutstandingCharArrays, stats.OutstandingObjects);

            // Memory pools are self-managing, but we can log stats for monitoring
            await Task.CompletedTask;
        }
    }

    private async Task CleanupColdStartTracker(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var coldStartTracker = scope.ServiceProvider.GetService<ColdStartTracker>();

        if (coldStartTracker != null)
        {
            // Cold start tracker is stateless after completion
            // This could be extended to reset tracking in test scenarios
            await Task.CompletedTask;
            Log.ColdStartTrackerCleanup(_logger);
        }
    }

    private async Task ForceGarbageCollection(CancellationToken cancellationToken)
    {
        var beforeMemory = GC.GetTotalMemory(false);

        // Force collection of all generations
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);

        // Allow some time for finalization
        await Task.Delay(100, cancellationToken);
        GC.WaitForPendingFinalizers();

        var afterMemory = GC.GetTotalMemory(false);
        var memoryFreed = beforeMemory - afterMemory;

        Log.GarbageCollectionCompleted(_logger, beforeMemory, afterMemory, memoryFreed);
    }

    public override void Dispose()
    {
        _cleanupTimer?.Dispose();
        base.Dispose();
        Log.BackgroundServiceDisposed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6401, Level = LogLevel.Information, Message = "Cleanup background service initialized with {IntervalMinutes:F1} minute intervals")]
        public static partial void BackgroundServiceInitialized(ILogger logger, double intervalMinutes);

        [LoggerMessage(EventId = 6402, Level = LogLevel.Information, Message = "Cleanup background service started")]
        public static partial void BackgroundServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 6403, Level = LogLevel.Information, Message = "Cleanup background service stopped")]
        public static partial void BackgroundServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 6404, Level = LogLevel.Information, Message = "Cleanup background service disposed")]
        public static partial void BackgroundServiceDisposed(ILogger logger);

        [LoggerMessage(EventId = 6405, Level = LogLevel.Information, Message = "Cleanup cycle started")]
        public static partial void CleanupCycleStarted(ILogger logger);

        [LoggerMessage(EventId = 6406, Level = LogLevel.Information, Message = "Cleanup cycle completed: {TasksCompleted} tasks completed, {TasksSkipped} skipped, duration {DurationMs:F1}ms")]
        public static partial void CleanupCycleCompleted(ILogger logger, int tasksCompleted, int tasksSkipped, double durationMs);

        [LoggerMessage(EventId = 6407, Level = LogLevel.Error, Message = "Cleanup cycle error occurred")]
        public static partial void CleanupCycleError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6408, Level = LogLevel.Information, Message = "Idempotency store cleanup completed: {CleanedCount} entries removed")]
        public static partial void IdempotencyStoreCleanup(ILogger logger, int cleanedCount);

        [LoggerMessage(EventId = 6409, Level = LogLevel.Error, Message = "Idempotency cleanup failed")]
        public static partial void IdempotencyCleanupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6410, Level = LogLevel.Information, Message = "Rate limit counters cleanup completed")]
        public static partial void RateLimitCountersCleanup(ILogger logger);

        [LoggerMessage(EventId = 6411, Level = LogLevel.Error, Message = "Rate limit cleanup failed")]
        public static partial void RateLimitCleanupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6412, Level = LogLevel.Information, Message = "Memory pool stats - Outstanding: {ByteArrays} byte arrays, {CharArrays} char arrays, {Objects} objects")]
        public static partial void MemoryPoolStats(ILogger logger, long byteArrays, long charArrays, long objects);

        [LoggerMessage(EventId = 6413, Level = LogLevel.Error, Message = "Memory pool cleanup failed")]
        public static partial void MemoryPoolCleanupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6414, Level = LogLevel.Information, Message = "Cold start tracker cleanup completed")]
        public static partial void ColdStartTrackerCleanup(ILogger logger);

        [LoggerMessage(EventId = 6415, Level = LogLevel.Error, Message = "Cold start cleanup failed")]
        public static partial void ColdStartCleanupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6416, Level = LogLevel.Information, Message = "Garbage collection completed: {BeforeBytes} -> {AfterBytes} bytes ({FreedBytes} freed)")]
        public static partial void GarbageCollectionCompleted(ILogger logger, long beforeBytes, long afterBytes, long freedBytes);

        [LoggerMessage(EventId = 6417, Level = LogLevel.Error, Message = "Garbage collection failed")]
        public static partial void GarbageCollectionFailed(ILogger logger, Exception exception);
    }
}

/// <summary>
/// Cleanup service configuration options
/// </summary>
public sealed class CleanupOptions
{
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    // Feature flags
    public bool EnableIdempotencyCleanup { get; set; } = true;
    public bool EnableRateLimitCleanup { get; set; } = true;
    public bool EnableMemoryPoolCleanup { get; set; } = true;
    public bool EnableColdStartCleanup { get; set; } = true;
    public bool EnableGarbageCollection { get; set; } // Disabled by default

    // Retention periods
    public TimeSpan IdempotencyRetentionPeriod { get; set; } = TimeSpan.FromHours(24);
}