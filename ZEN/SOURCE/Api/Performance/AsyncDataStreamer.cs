using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Performance;

/// <summary>
/// Async enumerable streaming for memory-efficient processing of large datasets
/// Provides backpressure control and cancellation support
/// </summary>
public sealed partial class AsyncDataStreamer<T>
{
    private readonly ILogger<AsyncDataStreamer<T>> _logger;
    private readonly IClock _clock;
    private readonly int _batchSize;
    private readonly TimeSpan _batchDelay;

    public AsyncDataStreamer(ILogger<AsyncDataStreamer<T>> logger, IClock clock, int batchSize = 100, TimeSpan? batchDelay = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        _logger = logger;
        _clock = clock;
        _batchSize = Math.Max(1, batchSize);
        _batchDelay = batchDelay ?? TimeSpan.FromMilliseconds(10);
    }

    /// <summary>
    /// Stream data asynchronously with memory-efficient batching
    /// </summary>
    public async IAsyncEnumerable<StreamBatch<T>> StreamAsync<TSource>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, T> transform,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);

        var batch = new List<T>(_batchSize);
        var batchCount = 0;
        var totalProcessed = 0;
        var startTime = _clock.UtcNow;

        Log.StreamingStarted(_logger, _batchSize, _batchDelay.TotalMilliseconds);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            T? transformed = default;
            var hasError = false;

            try
            {
                transformed = transform(item);
            }
            catch (Exception ex)
            {
                Log.TransformationError(_logger, ex, totalProcessed);
                hasError = true;
            }

            if (!hasError && transformed != null)
            {
                batch.Add(transformed);
                totalProcessed++;

                if (batch.Count >= _batchSize)
                {
                    yield return CreateBatch(batch, ++batchCount, totalProcessed);
                    batch.Clear();

                    // Backpressure control
                    if (_batchDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_batchDelay, cancellationToken);
                    }
                }
            }
        }

        // Yield remaining items
        if (batch.Count > 0)
        {
            yield return CreateBatch(batch, ++batchCount, totalProcessed);
        }

        var duration = _clock.UtcNow - startTime;
        Log.StreamingCompleted(_logger, totalProcessed, batchCount, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Stream from enumerable with async transformation
    /// </summary>
    public async IAsyncEnumerable<StreamBatch<T>> StreamWithAsyncTransformAsync<TSource>(
        IEnumerable<TSource> source,
        Func<TSource, Task<T>> transformAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transformAsync);

        var batch = new List<T>(_batchSize);
        var batchCount = 0;
        var totalProcessed = 0;
        var startTime = _clock.UtcNow;

        Log.AsyncStreamingStarted(_logger, _batchSize, _batchDelay.TotalMilliseconds);

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            T? transformed = default;
            var hasError = false;

            try
            {
                transformed = await transformAsync(item);
            }
            catch (Exception ex)
            {
                Log.AsyncTransformationError(_logger, ex, totalProcessed);
                hasError = true;
            }

            if (!hasError && transformed != null)
            {
                batch.Add(transformed);
                totalProcessed++;

                if (batch.Count >= _batchSize)
                {
                    yield return CreateBatch(batch, ++batchCount, totalProcessed);
                    batch.Clear();

                    if (_batchDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_batchDelay, cancellationToken);
                    }
                }
            }
        }

        if (batch.Count > 0)
        {
            yield return CreateBatch(batch, ++batchCount, totalProcessed);
        }

        var duration = _clock.UtcNow - startTime;
        Log.AsyncStreamingCompleted(_logger, totalProcessed, batchCount, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Create paginated async enumerable from offset-based data source
    /// </summary>
    public async IAsyncEnumerable<StreamBatch<T>> StreamPaginatedAsync(
        Func<int, int, CancellationToken, Task<IEnumerable<T>>> pageLoader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageLoader);

        var offset = 0;
        var batchCount = 0;
        var totalProcessed = 0;
        var startTime = _clock.UtcNow;

        Log.PaginatedStreamingStarted(_logger, _batchSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<T>? page = null;
            try
            {
                page = await pageLoader(offset, _batchSize, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.StreamingFailed(_logger, ex, totalProcessed, batchCount);
                throw;
            }

            var items = page.ToList();

            if (items.Count == 0)
            {
                break;
            }

            totalProcessed += items.Count;
            yield return CreateBatch(items, ++batchCount, totalProcessed);

            if (items.Count < _batchSize)
            {
                // Last page
                break;
            }

            offset += _batchSize;

            if (_batchDelay > TimeSpan.Zero)
            {
                await Task.Delay(_batchDelay, cancellationToken);
            }
        }

        var duration = _clock.UtcNow - startTime;
        Log.PaginatedStreamingCompleted(_logger, totalProcessed, batchCount, duration.TotalMilliseconds);
    }

    private StreamBatch<T> CreateBatch(List<T> items, int batchNumber, int totalProcessed)
    {
        return new StreamBatch<T>
        {
            Items = new ReadOnlyCollection<T>(new List<T>(items)), // Create defensive copy
            BatchNumber = batchNumber,
            BatchSize = items.Count,
            TotalProcessed = totalProcessed,
            Timestamp = _clock.UtcNow
        };
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6101, Level = LogLevel.Information, Message = "Streaming started with batch size {BatchSize}, delay {DelayMs}ms")]
        public static partial void StreamingStarted(ILogger logger, int batchSize, double delayMs);

        [LoggerMessage(EventId = 6102, Level = LogLevel.Information, Message = "Streaming completed: {TotalItems} items in {BatchCount} batches, duration {DurationMs:F1}ms")]
        public static partial void StreamingCompleted(ILogger logger, int totalItems, int batchCount, double durationMs);

        [LoggerMessage(EventId = 6103, Level = LogLevel.Warning, Message = "Streaming cancelled after processing {TotalItems} items in {BatchCount} batches")]
        public static partial void StreamingCancelled(ILogger logger, int totalItems, int batchCount);

        [LoggerMessage(EventId = 6104, Level = LogLevel.Error, Message = "Streaming failed after processing {TotalItems} items in {BatchCount} batches")]
        public static partial void StreamingFailed(ILogger logger, Exception exception, int totalItems, int batchCount);

        [LoggerMessage(EventId = 6105, Level = LogLevel.Warning, Message = "Transformation error at item {ItemIndex}")]
        public static partial void TransformationError(ILogger logger, Exception exception, int itemIndex);

        [LoggerMessage(EventId = 6106, Level = LogLevel.Information, Message = "Async streaming started with batch size {BatchSize}, delay {DelayMs}ms")]
        public static partial void AsyncStreamingStarted(ILogger logger, int batchSize, double delayMs);

        [LoggerMessage(EventId = 6107, Level = LogLevel.Information, Message = "Async streaming completed: {TotalItems} items in {BatchCount} batches, duration {DurationMs:F1}ms")]
        public static partial void AsyncStreamingCompleted(ILogger logger, int totalItems, int batchCount, double durationMs);

        [LoggerMessage(EventId = 6108, Level = LogLevel.Warning, Message = "Async transformation error at item {ItemIndex}")]
        public static partial void AsyncTransformationError(ILogger logger, Exception exception, int itemIndex);

        [LoggerMessage(EventId = 6109, Level = LogLevel.Information, Message = "Paginated streaming started with page size {PageSize}")]
        public static partial void PaginatedStreamingStarted(ILogger logger, int pageSize);

        [LoggerMessage(EventId = 6110, Level = LogLevel.Information, Message = "Paginated streaming completed: {TotalItems} items in {BatchCount} pages, duration {DurationMs:F1}ms")]
        public static partial void PaginatedStreamingCompleted(ILogger logger, int totalItems, int batchCount, double durationMs);
    }
}

/// <summary>
/// Batch of streamed items with metadata
/// </summary>
public sealed class StreamBatch<T>
{
    public required ReadOnlyCollection<T> Items { get; init; }
    public required int BatchNumber { get; init; }
    public required int BatchSize { get; init; }
    public required int TotalProcessed { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Stream processing options
/// </summary>
public sealed class StreamOptions
{
    public int BatchSize { get; init; } = 100;
    public TimeSpan BatchDelay { get; init; } = TimeSpan.FromMilliseconds(10);
    public bool ContinueOnError { get; init; } = true;
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
}