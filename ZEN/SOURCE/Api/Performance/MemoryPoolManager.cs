using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;

namespace RichMove.SmartPay.Api.Performance;

/// <summary>
/// Memory pooling for high-frequency objects to reduce GC pressure
/// Provides ArrayPool and custom object pools with metrics
/// </summary>
public sealed partial class MemoryPoolManager : IDisposable
{
    private readonly ILogger<MemoryPoolManager> _logger;
    private readonly MemoryPoolOptions _options;
    private readonly ArrayPool<byte> _byteArrayPool;
    private readonly ArrayPool<char> _charArrayPool;
    private readonly ConcurrentDictionary<Type, IObjectPool> _objectPools;
    private readonly PoolMetrics _metrics;
    private readonly Timer _metricsTimer;
    private bool _disposed;

    public MemoryPoolManager(ILogger<MemoryPoolManager> logger, IOptions<MemoryPoolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
        _byteArrayPool = ArrayPool<byte>.Create(_options.MaxByteArrayLength, _options.MaxArraysPerBucket);
        _charArrayPool = ArrayPool<char>.Create(_options.MaxCharArrayLength, _options.MaxArraysPerBucket);
        _objectPools = new ConcurrentDictionary<Type, IObjectPool>();
        _metrics = new PoolMetrics();

        _metricsTimer = new Timer(LogMetrics, null, _options.MetricsInterval, _options.MetricsInterval);

        Log.MemoryPoolManagerInitialized(_logger, _options.MaxByteArrayLength, _options.MaxCharArrayLength, _options.MaxArraysPerBucket);
    }

    /// <summary>
    /// Get pooled byte array
    /// </summary>
    public PooledByteArray GetByteArray(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);

        var array = _byteArrayPool.Rent(minimumLength);
        _metrics.IncrementByteArrayRental();

        Log.ByteArrayRented(_logger, array.Length, minimumLength);
        return new PooledByteArray(array, minimumLength, this);
    }

    /// <summary>
    /// Get pooled char array
    /// </summary>
    public PooledCharArray GetCharArray(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);

        var array = _charArrayPool.Rent(minimumLength);
        _metrics.IncrementCharArrayRental();

        Log.CharArrayRented(_logger, array.Length, minimumLength);
        return new PooledCharArray(array, minimumLength, this);
    }

    /// <summary>
    /// Get object pool for specific type
    /// </summary>
    public SmartPayObjectPool<T> GetObjectPool<T>() where T : class, new()
    {
        var type = typeof(T);

        if (_objectPools.TryGetValue(type, out var existingPool))
        {
            return (SmartPayObjectPool<T>)existingPool;
        }

        var newPool = new SmartPayObjectPool<T>(_logger, _options.MaxObjectsPerPool);
        _objectPools.TryAdd(type, newPool);

        Log.ObjectPoolCreated(_logger, type.Name, _options.MaxObjectsPerPool);
        return newPool;
    }

    /// <summary>
    /// Get pooled object of specified type
    /// </summary>
    public PooledObject<T> GetObject<T>() where T : class, new()
    {
        var pool = GetObjectPool<T>();
        var obj = pool.Rent();
        _metrics.IncrementObjectRental();

        return new PooledObject<T>(obj, pool);
    }

    internal void ReturnByteArray(byte[] array)
    {
        _byteArrayPool.Return(array, clearArray: _options.ClearArraysOnReturn);
        _metrics.IncrementByteArrayReturn();
        Log.ByteArrayReturned(_logger, array.Length);
    }

    internal void ReturnCharArray(char[] array)
    {
        _charArrayPool.Return(array, clearArray: _options.ClearArraysOnReturn);
        _metrics.IncrementCharArrayReturn();
        Log.CharArrayReturned(_logger, array.Length);
    }

    /// <summary>
    /// Get current pool statistics
    /// </summary>
    public PoolStatistics Statistics => new()
    {
        ByteArrayRentals = _metrics.ByteArrayRentals,
        ByteArrayReturns = _metrics.ByteArrayReturns,
        CharArrayRentals = _metrics.CharArrayRentals,
        CharArrayReturns = _metrics.CharArrayReturns,
        ObjectRentals = _metrics.ObjectRentals,
        ObjectReturns = _metrics.ObjectReturns,
        ActiveObjectPools = _objectPools.Count,
        OutstandingByteArrays = _metrics.ByteArrayRentals - _metrics.ByteArrayReturns,
        OutstandingCharArrays = _metrics.CharArrayRentals - _metrics.CharArrayReturns,
        OutstandingObjects = _metrics.ObjectRentals - _metrics.ObjectReturns
    };

    private void LogMetrics(object? state)
    {
        if (_disposed) return;

        var stats = Statistics;

        if (stats.OutstandingByteArrays > _options.OutstandingItemWarningThreshold ||
            stats.OutstandingCharArrays > _options.OutstandingItemWarningThreshold ||
            stats.OutstandingObjects > _options.OutstandingItemWarningThreshold)
        {
            Log.HighOutstandingItems(_logger, stats.OutstandingByteArrays, stats.OutstandingCharArrays, stats.OutstandingObjects);
        }

        Log.PoolMetrics(_logger, stats.ByteArrayRentals, stats.CharArrayRentals, stats.ObjectRentals,
                       stats.OutstandingByteArrays, stats.OutstandingCharArrays, stats.OutstandingObjects);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _metricsTimer?.Dispose();

        foreach (var pool in _objectPools.Values)
        {
            pool.Dispose();
        }

        _disposed = true;
        Log.MemoryPoolManagerDisposed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6301, Level = LogLevel.Information, Message = "Memory pool manager initialized: byte arrays up to {MaxByteLength}, char arrays up to {MaxCharLength}, {MaxPerBucket} per bucket")]
        public static partial void MemoryPoolManagerInitialized(ILogger logger, int maxByteLength, int maxCharLength, int maxPerBucket);

        [LoggerMessage(EventId = 6302, Level = LogLevel.Debug, Message = "Byte array rented: {ActualLength} bytes (requested {MinimumLength})")]
        public static partial void ByteArrayRented(ILogger logger, int actualLength, int minimumLength);

        [LoggerMessage(EventId = 6303, Level = LogLevel.Debug, Message = "Byte array returned: {Length} bytes")]
        public static partial void ByteArrayReturned(ILogger logger, int length);

        [LoggerMessage(EventId = 6304, Level = LogLevel.Debug, Message = "Char array rented: {ActualLength} chars (requested {MinimumLength})")]
        public static partial void CharArrayRented(ILogger logger, int actualLength, int minimumLength);

        [LoggerMessage(EventId = 6305, Level = LogLevel.Debug, Message = "Char array returned: {Length} chars")]
        public static partial void CharArrayReturned(ILogger logger, int length);

        [LoggerMessage(EventId = 6306, Level = LogLevel.Information, Message = "Object pool created for {TypeName} with max {MaxObjects} objects")]
        public static partial void ObjectPoolCreated(ILogger logger, string typeName, int maxObjects);

        [LoggerMessage(EventId = 6307, Level = LogLevel.Warning, Message = "High outstanding pool items detected - Byte arrays: {OutstandingBytes}, Char arrays: {OutstandingChars}, Objects: {OutstandingObjects}")]
        public static partial void HighOutstandingItems(ILogger logger, long outstandingBytes, long outstandingChars, long outstandingObjects);

        [LoggerMessage(EventId = 6308, Level = LogLevel.Information, Message = "Pool metrics - Rentals: {ByteRentals} bytes, {CharRentals} chars, {ObjectRentals} objects | Outstanding: {OutstandingBytes}, {OutstandingChars}, {OutstandingObjects}")]
        public static partial void PoolMetrics(ILogger logger, long byteRentals, long charRentals, long objectRentals, long outstandingBytes, long outstandingChars, long outstandingObjects);

        [LoggerMessage(EventId = 6309, Level = LogLevel.Information, Message = "Memory pool manager disposed")]
        public static partial void MemoryPoolManagerDisposed(ILogger logger);
    }
}

/// <summary>
/// Pooled byte array with automatic return
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Performance-critical struct designed for using statements")]
public readonly struct PooledByteArray : IDisposable
{
    private readonly byte[] _array;
    private readonly int _length;
    private readonly MemoryPoolManager _manager;

    internal PooledByteArray(byte[] array, int length, MemoryPoolManager manager)
    {
        _array = array;
        _length = length;
        _manager = manager;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Performance-critical pooled array access")]
    public byte[] Array => _array;
    public int Length => _length;
    public Span<byte> Span => _array.AsSpan(0, _length);

    public void Dispose()
    {
        _manager.ReturnByteArray(_array);
    }
}

/// <summary>
/// Pooled char array with automatic return
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Performance-critical struct designed for using statements")]
public readonly struct PooledCharArray : IDisposable
{
    private readonly char[] _array;
    private readonly int _length;
    private readonly MemoryPoolManager _manager;

    internal PooledCharArray(char[] array, int length, MemoryPoolManager manager)
    {
        _array = array;
        _length = length;
        _manager = manager;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Performance-critical pooled array access")]
    public char[] Array => _array;
    public int Length => _length;
    public Span<char> Span => _array.AsSpan(0, _length);

    public void Dispose()
    {
        _manager.ReturnCharArray(_array);
    }
}

/// <summary>
/// Pooled object with automatic return
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Performance-critical struct designed for using statements")]
public readonly struct PooledObject<T> : IDisposable where T : class, new()
{
    private readonly T _object;
    private readonly SmartPayObjectPool<T> _pool;

    internal PooledObject(T obj, SmartPayObjectPool<T> pool)
    {
        _object = obj;
        _pool = pool;
    }

    public T Value => _object;

    public void Dispose()
    {
        _pool.Return(_object);
    }
}

/// <summary>
/// Object pool for specific type
/// </summary>
public sealed class SmartPayObjectPool<T> : IObjectPool where T : class, new()
{
    private readonly ConcurrentQueue<T> _objects = new();
    private readonly ILogger _logger;
    private readonly int _maxObjects;
    private int _currentCount;

    internal SmartPayObjectPool(ILogger logger, int maxObjects)
    {
        _logger = logger;
        _maxObjects = maxObjects;
    }

    public T Rent()
    {
        if (_objects.TryDequeue(out var obj))
        {
            Interlocked.Decrement(ref _currentCount);
            return obj;
        }

        return new T();
    }

    public void Return(T obj)
    {
        if (obj == null) return;

        // Reset object if it implements IResettable
        if (obj is IResettable resettable)
        {
            resettable.Reset();
        }

        if (_currentCount < _maxObjects)
        {
            _objects.Enqueue(obj);
            Interlocked.Increment(ref _currentCount);
        }
    }

    public void Dispose()
    {
        while (_objects.TryDequeue(out var obj))
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Interface for resettable objects
/// </summary>
public interface IResettable
{
    void Reset();
}

/// <summary>
/// Base interface for object pools
/// </summary>
internal interface IObjectPool : IDisposable
{
}

/// <summary>
/// Pool metrics tracking
/// </summary>
internal sealed class PoolMetrics
{

    private long _byteArrayRentals;
    private long _byteArrayReturns;
    private long _charArrayRentals;
    private long _charArrayReturns;
    private long _objectRentals;
    private long _objectReturns;

    public long ByteArrayRentals => _byteArrayRentals;
    public long ByteArrayReturns => _byteArrayReturns;
    public long CharArrayRentals => _charArrayRentals;
    public long CharArrayReturns => _charArrayReturns;
    public long ObjectRentals => _objectRentals;
    public long ObjectReturns => _objectReturns;

    public void IncrementByteArrayRental() => Interlocked.Increment(ref _byteArrayRentals);
    public void IncrementByteArrayReturn() => Interlocked.Increment(ref _byteArrayReturns);
    public void IncrementCharArrayRental() => Interlocked.Increment(ref _charArrayRentals);
    public void IncrementCharArrayReturn() => Interlocked.Increment(ref _charArrayReturns);
    public void IncrementObjectRental() => Interlocked.Increment(ref _objectRentals);
    public void IncrementObjectReturn() => Interlocked.Increment(ref _objectReturns);
}

/// <summary>
/// Memory pool configuration options
/// </summary>
public sealed class MemoryPoolOptions
{
    public int MaxByteArrayLength { get; set; } = 1024 * 1024; // 1MB
    public int MaxCharArrayLength { get; set; } = 1024 * 1024; // 1M chars
    public int MaxArraysPerBucket { get; set; } = 50;
    public int MaxObjectsPerPool { get; set; } = 100;
    public bool ClearArraysOnReturn { get; set; } = true;
    public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int OutstandingItemWarningThreshold { get; set; } = 1000;
}

/// <summary>
/// Pool statistics
/// </summary>
public sealed class PoolStatistics
{
    public long ByteArrayRentals { get; init; }
    public long ByteArrayReturns { get; init; }
    public long CharArrayRentals { get; init; }
    public long CharArrayReturns { get; init; }
    public long ObjectRentals { get; init; }
    public long ObjectReturns { get; init; }
    public int ActiveObjectPools { get; init; }
    public long OutstandingByteArrays { get; init; }
    public long OutstandingCharArrays { get; init; }
    public long OutstandingObjects { get; init; }
}