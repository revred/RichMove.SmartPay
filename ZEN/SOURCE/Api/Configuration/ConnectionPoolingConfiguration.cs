using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Api.Configuration;

/// <summary>
/// Connection pooling optimization for HttpClient and database connections
/// Provides configuration for efficient resource utilization
/// </summary>
public static partial class ConnectionPoolingConfiguration
{
    /// <summary>
    /// Configure optimized HttpClient with connection pooling
    /// </summary>
    public static IServiceCollection AddOptimizedHttpClients(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // Configure global connection pooling options
        services.Configure<ConnectionPoolOptions>(config.GetSection("ConnectionPooling"));

        // Primary HTTP client for external APIs
        services.AddHttpClient("external-api", (serviceProvider, client) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<HttpClient>>();
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>().Value;

            client.Timeout = options.HttpClientTimeout;
            client.DefaultRequestHeaders.Add("User-Agent", "SmartPay/1.0");

            Log.HttpClientConfigured(logger, "external-api", options.HttpClientTimeout.TotalSeconds, options.MaxConnectionsPerServer);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>().Value;
            return new HttpClientHandler
            {
                MaxConnectionsPerServer = options.MaxConnectionsPerServer,
                MaxResponseHeadersLength = options.MaxResponseHeadersLength,
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                {
                    return options.AllowInvalidCertificates || sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                }
            };
        });

        // Specialized client for blockchain operations
        services.AddHttpClient("blockchain", (serviceProvider, client) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<HttpClient>>();
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>().Value;

            client.Timeout = options.BlockchainClientTimeout;
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            Log.HttpClientConfigured(logger, "blockchain", options.BlockchainClientTimeout.TotalSeconds, options.MaxConnectionsPerServer);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>().Value;
            return new HttpClientHandler
            {
                MaxConnectionsPerServer = options.BlockchainMaxConnections
            };
        });

        // Health check client with minimal pooling
        services.AddHttpClient("health-check", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>().Value;
            client.Timeout = options.HealthCheckTimeout;
        })
        .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
        {
            MaxConnectionsPerServer = 2
        });

        return services;
    }

    /// <summary>
    /// Configure database connection pooling (placeholder for Entity Framework)
    /// </summary>
    public static IServiceCollection AddOptimizedDatabaseConnections(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<DatabasePoolOptions>(config.GetSection("DatabasePooling"));

        // This is a placeholder for database connection pooling configuration
        // In a real implementation, this would configure Entity Framework or other ORMs
        services.AddSingleton<DatabaseConnectionPool>();

        return services;
    }

    // Note: Polly policy handlers are not configured in this version
    // They can be added via separate NuGet packages if needed

    private static partial class Log
    {
        [LoggerMessage(EventId = 6501, Level = LogLevel.Information, Message = "HTTP client '{ClientName}' configured: timeout {TimeoutSeconds}s, max connections {MaxConnections}")]
        public static partial void HttpClientConfigured(ILogger logger, string clientName, double timeoutSeconds, int maxConnections);

        [LoggerMessage(EventId = 6502, Level = LogLevel.Warning, Message = "HTTP retry attempt {RetryCount} after {DelaySeconds}s")]
        public static partial void HttpRetryAttempt(ILogger logger, int retryCount, double delaySeconds);

        [LoggerMessage(EventId = 6503, Level = LogLevel.Error, Message = "HTTP timeout after {TimeoutSeconds}s")]
        public static partial void HttpTimeout(ILogger logger, double timeoutSeconds);

        [LoggerMessage(EventId = 6504, Level = LogLevel.Warning, Message = "Blockchain retry attempt {RetryCount} after {DelaySeconds}s")]
        public static partial void BlockchainRetryAttempt(ILogger logger, int retryCount, double delaySeconds);

        [LoggerMessage(EventId = 6505, Level = LogLevel.Information, Message = "Database connection pool configured: min {MinConnections}, max {MaxConnections}, timeout {TimeoutSeconds}s")]
        public static partial void DatabasePoolConfigured(ILogger logger, int minConnections, int maxConnections, double timeoutSeconds);
    }
}

/// <summary>
/// HTTP connection pooling options
/// </summary>
public sealed class ConnectionPoolOptions
{
    // General HTTP client settings
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConnectionsPerServer { get; set; } = 10;
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public int MaxResponseHeadersLength { get; set; } = 64 * 1024; // 64KB
    public bool AllowInvalidCertificates { get; set; }

    // Blockchain-specific settings
    public TimeSpan BlockchainClientTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public int BlockchainMaxConnections { get; set; } = 5;
    public TimeSpan BlockchainConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);

    // Health check settings
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Database connection pooling options
/// </summary>
public sealed class DatabasePoolOptions
{
    public int MinConnections { get; set; } = 5;
    public int MaxConnections { get; set; } = 50;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public bool EnableConnectionPooling { get; set; } = true;
    public int ConnectionRetryCount { get; set; } = 3;
    public TimeSpan ConnectionRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Database connection pool (placeholder implementation)
/// </summary>
public sealed partial class DatabaseConnectionPool : IDisposable
{
    private readonly ILogger<DatabaseConnectionPool> _logger;
    private readonly DatabasePoolOptions _options;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly Timer _maintenanceTimer;
    private bool _disposed;

    public DatabaseConnectionPool(ILogger<DatabaseConnectionPool> logger, IOptions<DatabasePoolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
        _connectionSemaphore = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);

        // Periodic maintenance timer
        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        Log.DatabasePoolInitialized(_logger, _options.MinConnections, _options.MaxConnections, _options.ConnectionTimeout.TotalSeconds);
    }

    /// <summary>
    /// Connection pool statistics
    /// </summary>
    public ConnectionPoolStatistics Statistics => new()
    {
        MaxConnections = _options.MaxConnections,
        AvailableConnections = _connectionSemaphore.CurrentCount,
        ActiveConnections = _options.MaxConnections - _connectionSemaphore.CurrentCount,
        MinConnections = _options.MinConnections
    };

    private void PerformMaintenance(object? state)
    {
        if (_disposed) return;

        var stats = Statistics;
        Log.DatabasePoolMaintenance(_logger, stats.ActiveConnections, stats.AvailableConnections);

        // Placeholder for connection maintenance logic
        // In a real implementation, this would clean up idle connections, validate connections, etc.
    }

    public void Dispose()
    {
        if (_disposed) return;

        _maintenanceTimer?.Dispose();
        _connectionSemaphore?.Dispose();
        _disposed = true;

        Log.DatabasePoolDisposed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6510, Level = LogLevel.Information, Message = "Database connection pool initialized: min {MinConnections}, max {MaxConnections}, timeout {TimeoutSeconds}s")]
        public static partial void DatabasePoolInitialized(ILogger logger, int minConnections, int maxConnections, double timeoutSeconds);

        [LoggerMessage(EventId = 6511, Level = LogLevel.Debug, Message = "Database pool maintenance: {ActiveConnections} active, {AvailableConnections} available")]
        public static partial void DatabasePoolMaintenance(ILogger logger, int activeConnections, int availableConnections);

        [LoggerMessage(EventId = 6512, Level = LogLevel.Information, Message = "Database connection pool disposed")]
        public static partial void DatabasePoolDisposed(ILogger logger);
    }
}

/// <summary>
/// Connection pool statistics
/// </summary>
public sealed class ConnectionPoolStatistics
{
    public int MaxConnections { get; init; }
    public int AvailableConnections { get; init; }
    public int ActiveConnections { get; init; }
    public int MinConnections { get; init; }
}