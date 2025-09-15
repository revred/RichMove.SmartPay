using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Configuration;

/// <summary>
/// Secure configuration management with hierarchical overrides and secret handling
/// Provides encrypted configuration storage and hot-reload capabilities for production
/// </summary>
public sealed partial class SecureConfigurationManager : IDisposable
{
    private readonly ILogger<SecureConfigurationManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConfigurationSecurityOptions _securityOptions;
    private readonly ConcurrentDictionary<string, ConfigurationValue> _secureCache;
    private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps;
    private readonly Timer? _refreshTimer;
    private readonly SemaphoreSlim _refreshSemaphore;
    private bool _disposed;

    public SecureConfigurationManager(
        ILogger<SecureConfigurationManager> logger,
        IConfiguration configuration,
        IOptions<ConfigurationSecurityOptions> securityOptions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(securityOptions);

        _logger = logger;
        _configuration = configuration;
        _securityOptions = securityOptions.Value;
        _secureCache = new ConcurrentDictionary<string, ConfigurationValue>();
        _cacheTimestamps = new ConcurrentDictionary<string, DateTime>();
        _refreshSemaphore = new SemaphoreSlim(1, 1);

        // Setup hot-reload timer if enabled
        _refreshTimer = _securityOptions.EnableHotReload
            ? new Timer(RefreshConfigurationAsync, null, _securityOptions.RefreshInterval, _securityOptions.RefreshInterval)
            : null;

        Log.ConfigurationManagerInitialized(_logger, _securityOptions.EnableHotReload, _securityOptions.RefreshInterval.TotalMinutes);
    }

    /// <summary>
    /// Get configuration value with automatic decryption for sensitive data
    /// </summary>
    public async Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            // Check cache first
            if (_secureCache.TryGetValue(key, out var cachedValue) &&
                _cacheTimestamps.TryGetValue(key, out var cacheTime) &&
                DateTime.UtcNow - cacheTime < _securityOptions.CacheExpiry)
            {
                Log.ConfigurationCacheHit(_logger, key);
                return DeserializeValue<T>(cachedValue.Value);
            }

            // Retrieve from configuration provider
            var configValue = await RetrieveConfigurationValueAsync(key, cancellationToken);
            if (configValue == null)
            {
                Log.ConfigurationKeyNotFound(_logger, key);
                return defaultValue;
            }

            // Process sensitive configuration
            var processedValue = IsSensitiveKey(key)
                ? await DecryptSensitiveValueAsync(configValue, cancellationToken)
                : configValue;

            // Cache the result
            _secureCache.AddOrUpdate(key, new ConfigurationValue { Value = processedValue, IsEncrypted = IsSensitiveKey(key) },
                (_, _) => new ConfigurationValue { Value = processedValue, IsEncrypted = IsSensitiveKey(key) });
            _cacheTimestamps.AddOrUpdate(key, DateTime.UtcNow, (_, _) => DateTime.UtcNow);

            Log.ConfigurationValueRetrieved(_logger, key, IsSensitiveKey(key));
            return DeserializeValue<T>(processedValue);
        }
        catch (Exception ex)
        {
            Log.ConfigurationRetrievalError(_logger, key, ex);
            return defaultValue;
        }
    }

    /// <summary>
    /// Set configuration value with automatic encryption for sensitive data
    /// </summary>
    public async Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            var serializedValue = SerializeValue(value);

            // Encrypt sensitive values
            var finalValue = IsSensitiveKey(key)
                ? await EncryptSensitiveValueAsync(serializedValue, cancellationToken)
                : serializedValue;

            // For MVP: Store in memory cache (in production: persist to secure store)
            _secureCache.AddOrUpdate(key, new ConfigurationValue { Value = finalValue, IsEncrypted = IsSensitiveKey(key) },
                (_, _) => new ConfigurationValue { Value = finalValue, IsEncrypted = IsSensitiveKey(key) });
            _cacheTimestamps.AddOrUpdate(key, DateTime.UtcNow, (_, _) => DateTime.UtcNow);

            Log.ConfigurationValueSet(_logger, key, IsSensitiveKey(key));
        }
        catch (Exception ex)
        {
            Log.ConfigurationSetError(_logger, key, ex);
            throw;
        }
    }

    /// <summary>
    /// Get environment-specific configuration with hierarchical override
    /// </summary>
    public async Task<ConfigurationHierarchy> GetHierarchicalConfigAsync(string baseKey, CancellationToken cancellationToken = default)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var hierarchy = new ConfigurationHierarchy { Environment = environment };

        // Base configuration
        var baseValue = await GetValueAsync<string>($"{baseKey}", cancellationToken: cancellationToken);
        if (baseValue != null) hierarchy.BaseValue = baseValue;

        // Environment-specific override
        var envValue = await GetValueAsync<string>($"{baseKey}:{environment}", cancellationToken: cancellationToken);
        if (envValue != null) hierarchy.EnvironmentValue = envValue;

        // Local development override (highest priority)
        var localValue = await GetValueAsync<string>($"{baseKey}:Local", cancellationToken: cancellationToken);
        if (localValue != null) hierarchy.LocalValue = localValue;

        // Determine effective value (local > environment > base)
        hierarchy.EffectiveValue = localValue ?? envValue ?? baseValue;

        Log.HierarchicalConfigResolved(_logger, baseKey, environment, hierarchy.EffectiveValue != null);
        return hierarchy;
    }

    /// <summary>
    /// Validate configuration schema and constraints
    /// </summary>
    public async Task<ValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Validate required configuration keys
            foreach (var requiredKey in _securityOptions.RequiredConfigurationKeys)
            {
                var value = await GetValueAsync<string>(requiredKey, cancellationToken: cancellationToken);
                if (string.IsNullOrEmpty(value))
                {
                    errors.Add($"Required configuration key '{requiredKey}' is missing or empty");
                }
            }

            // Validate sensitive configuration encryption
            foreach (var sensitiveKey in _securityOptions.SensitiveConfigurationKeys)
            {
                if (_secureCache.TryGetValue(sensitiveKey, out var configValue) && !configValue.IsEncrypted)
                {
                    warnings.Add($"Sensitive configuration key '{sensitiveKey}' is not encrypted");
                }
            }

            // Validate configuration constraints
            await ValidateConfigurationConstraintsAsync(errors, warnings, cancellationToken);

            var isValid = errors.Count == 0;
            Log.ConfigurationValidationCompleted(_logger, isValid, errors.Count, warnings.Count);

            return new ValidationResult
            {
                IsValid = isValid,
                Errors = errors.AsReadOnly(),
                Warnings = warnings.AsReadOnly(),
                ValidationTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Log.ConfigurationValidationError(_logger, ex);
            errors.Add($"Configuration validation failed: {ex.Message}");
            return new ValidationResult { IsValid = false, Errors = errors.AsReadOnly(), Warnings = warnings.AsReadOnly() };
        }
    }

    private async Task<string?> RetrieveConfigurationValueAsync(string key, CancellationToken cancellationToken)
    {
        // For MVP: Use standard IConfiguration
        // In production: Could integrate with Azure Key Vault, AWS Secrets Manager, etc.
        await Task.CompletedTask; // Placeholder for async config retrieval
        return _configuration[key];
    }

    private async Task<string> EncryptSensitiveValueAsync(string value, CancellationToken cancellationToken)
    {
        if (!_securityOptions.EnableEncryption)
            return value;

        try
        {
            await Task.CompletedTask; // Placeholder for async encryption

            // Simple encryption for MVP (in production: use proper key management)
            var bytes = Encoding.UTF8.GetBytes(value);
            var key = Encoding.UTF8.GetBytes(_securityOptions.EncryptionKey.PadRight(32)[..32]);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            var result = Convert.ToBase64String(aes.IV.Concat(encrypted).ToArray());
            Log.ConfigurationValueEncrypted(_logger, value.Length);
            return result;
        }
        catch (Exception ex)
        {
            Log.ConfigurationEncryptionError(_logger, ex);
            throw;
        }
    }

    private async Task<string> DecryptSensitiveValueAsync(string encryptedValue, CancellationToken cancellationToken)
    {
        if (!_securityOptions.EnableEncryption)
            return encryptedValue;

        try
        {
            await Task.CompletedTask; // Placeholder for async decryption

            var combined = Convert.FromBase64String(encryptedValue);
            var key = Encoding.UTF8.GetBytes(_securityOptions.EncryptionKey.PadRight(32)[..32]);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = combined[..16];

            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(combined, 16, combined.Length - 16);

            var result = Encoding.UTF8.GetString(decrypted);
            Log.ConfigurationValueDecrypted(_logger, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            Log.ConfigurationDecryptionError(_logger, ex);
            throw;
        }
    }

    private bool IsSensitiveKey(string key)
    {
        return _securityOptions.SensitiveConfigurationKeys.Any(pattern =>
            key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string SerializeValue<T>(T value)
    {
        return value switch
        {
            string s => s,
            null => string.Empty,
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static T? DeserializeValue<T>(string value)
    {
        if (string.IsNullOrEmpty(value))
            return default;

        if (typeof(T) == typeof(string))
            return (T)(object)value;

        try
        {
            return JsonSerializer.Deserialize<T>(value);
        }
        catch
        {
            return default;
        }
    }

    private async Task ValidateConfigurationConstraintsAsync(List<string> errors, List<string> warnings, CancellationToken cancellationToken)
    {
        // Validate connection strings
        var connectionString = await GetValueAsync<string>("ConnectionStrings:DefaultConnection", cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(connectionString) && !IsValidConnectionString(connectionString))
        {
            warnings.Add("Connection string format may be invalid");
        }

        // Validate URL configurations
        var apiBaseUrl = await GetValueAsync<string>("ApiSettings:BaseUrl", cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(apiBaseUrl) && !Uri.IsWellFormedUriString(apiBaseUrl, UriKind.Absolute))
        {
            errors.Add("API base URL is not a valid absolute URI");
        }
    }

    private static bool IsValidConnectionString(string connectionString)
    {
        // Basic connection string validation
        return connectionString.Contains('=', StringComparison.Ordinal) &&
               connectionString.Length > 10;
    }

    private async void RefreshConfigurationAsync(object? state)
    {
        if (_disposed || !await _refreshSemaphore.WaitAsync(100))
            return;

        try
        {
            // Clear expired cache entries
            var expiredKeys = _cacheTimestamps
                .Where(kvp => DateTime.UtcNow - kvp.Value > _securityOptions.CacheExpiry)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _secureCache.TryRemove(key, out _);
                _cacheTimestamps.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                Log.ConfigurationCacheRefreshed(_logger, expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            Log.ConfigurationRefreshError(_logger, ex);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _refreshTimer?.Dispose();
        _refreshSemaphore?.Dispose();
        _secureCache.Clear();
        _cacheTimestamps.Clear();
        _disposed = true;

        Log.ConfigurationManagerDisposed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 8101, Level = LogLevel.Information, Message = "Secure configuration manager initialized: hot-reload {HotReload}, refresh interval {RefreshMinutes}min")]
        public static partial void ConfigurationManagerInitialized(ILogger logger, bool hotReload, double refreshMinutes);

        [LoggerMessage(EventId = 8102, Level = LogLevel.Debug, Message = "Configuration cache hit for key: {Key}")]
        public static partial void ConfigurationCacheHit(ILogger logger, string key);

        [LoggerMessage(EventId = 8103, Level = LogLevel.Warning, Message = "Configuration key not found: {Key}")]
        public static partial void ConfigurationKeyNotFound(ILogger logger, string key);

        [LoggerMessage(EventId = 8104, Level = LogLevel.Information, Message = "Configuration value retrieved: {Key}, encrypted: {IsEncrypted}")]
        public static partial void ConfigurationValueRetrieved(ILogger logger, string key, bool isEncrypted);

        [LoggerMessage(EventId = 8105, Level = LogLevel.Error, Message = "Configuration retrieval error for key: {Key}")]
        public static partial void ConfigurationRetrievalError(ILogger logger, string key, Exception exception);

        [LoggerMessage(EventId = 8106, Level = LogLevel.Information, Message = "Configuration value set: {Key}, encrypted: {IsEncrypted}")]
        public static partial void ConfigurationValueSet(ILogger logger, string key, bool isEncrypted);

        [LoggerMessage(EventId = 8107, Level = LogLevel.Error, Message = "Configuration set error for key: {Key}")]
        public static partial void ConfigurationSetError(ILogger logger, string key, Exception exception);

        [LoggerMessage(EventId = 8108, Level = LogLevel.Information, Message = "Hierarchical config resolved: {BaseKey}, environment: {Environment}, found: {HasValue}")]
        public static partial void HierarchicalConfigResolved(ILogger logger, string baseKey, string environment, bool hasValue);

        [LoggerMessage(EventId = 8109, Level = LogLevel.Information, Message = "Configuration validation completed: valid {IsValid}, errors {ErrorCount}, warnings {WarningCount}")]
        public static partial void ConfigurationValidationCompleted(ILogger logger, bool isValid, int errorCount, int warningCount);

        [LoggerMessage(EventId = 8110, Level = LogLevel.Error, Message = "Configuration validation error")]
        public static partial void ConfigurationValidationError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 8111, Level = LogLevel.Debug, Message = "Configuration value encrypted: {OriginalLength} bytes")]
        public static partial void ConfigurationValueEncrypted(ILogger logger, int originalLength);

        [LoggerMessage(EventId = 8112, Level = LogLevel.Debug, Message = "Configuration value decrypted: {DecryptedLength} bytes")]
        public static partial void ConfigurationValueDecrypted(ILogger logger, int decryptedLength);

        [LoggerMessage(EventId = 8113, Level = LogLevel.Error, Message = "Configuration encryption error")]
        public static partial void ConfigurationEncryptionError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 8114, Level = LogLevel.Error, Message = "Configuration decryption error")]
        public static partial void ConfigurationDecryptionError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 8115, Level = LogLevel.Information, Message = "Configuration cache refreshed: {ExpiredCount} entries removed")]
        public static partial void ConfigurationCacheRefreshed(ILogger logger, int expiredCount);

        [LoggerMessage(EventId = 8116, Level = LogLevel.Error, Message = "Configuration refresh error")]
        public static partial void ConfigurationRefreshError(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 8117, Level = LogLevel.Information, Message = "Secure configuration manager disposed")]
        public static partial void ConfigurationManagerDisposed(ILogger logger);
    }
}

public sealed class ConfigurationValue
{
    public required string Value { get; init; }
    public bool IsEncrypted { get; init; }
}

public sealed class ConfigurationHierarchy
{
    public required string Environment { get; init; }
    public string? BaseValue { get; set; }
    public string? EnvironmentValue { get; set; }
    public string? LocalValue { get; set; }
    public string? EffectiveValue { get; set; }
}

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public DateTime ValidationTime { get; init; }
}

public sealed class ConfigurationSecurityOptions
{
    public bool EnableEncryption { get; set; } = true;
    public string EncryptionKey { get; set; } = "SmartPayDefaultKey123456789012"; // Should be from secure source
    public bool EnableHotReload { get; set; } = true;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(15);

    public IReadOnlyList<string> SensitiveConfigurationKeys { get; set; } = new[]
    {
        "password", "secret", "key", "token", "connectionstring", "apikey"
    };

    public IReadOnlyList<string> RequiredConfigurationKeys { get; set; } = new[]
    {
        "ConnectionStrings:DefaultConnection",
        "ApiSettings:BaseUrl"
    };
}