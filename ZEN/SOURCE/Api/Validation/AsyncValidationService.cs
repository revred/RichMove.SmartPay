using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace RichMove.SmartPay.Api.Validation;

/// <summary>
/// Async validation service for MVP - no Redis dependency
/// Uses simple in-memory caching with Supabase database validation
/// </summary>
public sealed partial class AsyncValidationService
{
    private readonly ILogger<AsyncValidationService> _logger;
    private readonly ConcurrentDictionary<string, (bool IsValid, DateTime CachedAt)> _validationCache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public AsyncValidationService(ILogger<AsyncValidationService> logger)
    {
        _logger = logger;
        _validationCache = new ConcurrentDictionary<string, (bool, DateTime)>();
    }

    /// <summary>
    /// Validate wallet address format and existence
    /// </summary>
    public async Task<ValidationResult> ValidateWalletAddressAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(walletAddress))
        {
            return ValidationResult.Invalid("Wallet address is required");
        }

        // Basic format validation
        if (!IsValidWalletFormat(walletAddress))
        {
            Log.InvalidWalletFormat(_logger, walletAddress);
            return ValidationResult.Invalid("Invalid wallet address format");
        }

        // Check cache first (simple in-memory for MVP)
        var cacheKey = $"wallet:{walletAddress}";
        if (_validationCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < _cacheExpiry)
        {
            Log.ValidationCacheHit(_logger, cacheKey);
            return cached.IsValid
                ? ValidationResult.Valid()
                : ValidationResult.Invalid("Wallet address not found");
        }

        // For MVP: Basic validation without database lookup
        // In production, this would check Supabase database
        var isValid = await ValidateWalletExistsAsync(walletAddress, cancellationToken);

        // Cache result
        _validationCache.AddOrUpdate(cacheKey, (isValid, DateTime.UtcNow), (_, _) => (isValid, DateTime.UtcNow));

        Log.WalletValidationCompleted(_logger, walletAddress, isValid);
        return isValid
            ? ValidationResult.Valid()
            : ValidationResult.Invalid("Wallet address not found");
    }

    /// <summary>
    /// Validate currency code against supported currencies
    /// </summary>
    public async Task<ValidationResult> ValidateCurrencyAsync(string currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currency))
        {
            return ValidationResult.Invalid("Currency is required");
        }

        // Normalize currency code
        var normalizedCurrency = currency.ToUpperInvariant();

        // Check cache
        var cacheKey = $"currency:{normalizedCurrency}";
        if (_validationCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < _cacheExpiry)
        {
            return cached.IsValid
                ? ValidationResult.Valid()
                : ValidationResult.Invalid("Unsupported currency");
        }

        // For MVP: Simple hardcoded validation
        // In production, this would check Supabase database
        var supportedCurrencies = new[] { "USD", "EUR", "GBP", "BTC", "ETH", "USDC" };
        var isValid = supportedCurrencies.Contains(normalizedCurrency);

        // Cache result
        _validationCache.AddOrUpdate(cacheKey, (isValid, DateTime.UtcNow), (_, _) => (isValid, DateTime.UtcNow));

        await Task.Delay(1, cancellationToken); // Simulate async operation

        Log.CurrencyValidationCompleted(_logger, currency, isValid);
        return isValid
            ? ValidationResult.Valid()
            : ValidationResult.Invalid($"Currency '{currency}' is not supported");
    }

    /// <summary>
    /// Validate payment amount against business rules
    /// </summary>
    public Task<ValidationResult> ValidatePaymentAmountAsync(decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Basic amount validation
        if (amount <= 0)
        {
            errors.Add("Amount must be positive");
        }

        // Currency-specific limits for MVP
        var maxAmount = currency?.ToUpperInvariant() switch
        {
            "BTC" => 10m,
            "ETH" => 100m,
            "USDC" => 50_000m,
            _ => 10_000m // USD, EUR, GBP default
        };

        if (amount > maxAmount)
        {
            errors.Add($"Amount exceeds maximum limit of {maxAmount} {currency}");
        }

        // Minimum amount check
        var minAmount = currency?.ToUpperInvariant() switch
        {
            "BTC" => 0.00001m,
            "ETH" => 0.001m,
            _ => 0.01m
        };

        if (amount < minAmount)
        {
            errors.Add($"Amount below minimum of {minAmount} {currency}");
        }

        Log.AmountValidationCompleted(_logger, amount, currency ?? "unknown", errors.Count == 0);

        return Task.FromResult(errors.Count == 0
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(string.Join("; ", errors)));
    }

    /// <summary>
    /// Clean expired cache entries
    /// </summary>
    public void CleanExpiredCache()
    {
        var expiredKeys = _validationCache
            .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt > _cacheExpiry)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _validationCache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            Log.CacheEntriesExpired(_logger, expiredKeys.Count);
        }
    }

    private static bool IsValidWalletFormat(string walletAddress)
    {
        // Basic Ethereum wallet format validation
        return walletAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               walletAddress.Length == 42 &&
               walletAddress[2..].All(c => char.IsAsciiHexDigit(c));
    }

    private static async Task<bool> ValidateWalletExistsAsync(string walletAddress, CancellationToken cancellationToken)
    {
        // For MVP: Simple format validation only
        // In production: Query Supabase database for wallet existence
        // Example: SELECT EXISTS(SELECT 1 FROM wallets WHERE address = walletAddress)

        await Task.Delay(10, cancellationToken); // Simulate database lookup

        // For MVP, assume properly formatted wallets exist
        return IsValidWalletFormat(walletAddress);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 7401, Level = LogLevel.Warning, Message = "Invalid wallet format: {WalletAddress}")]
        public static partial void InvalidWalletFormat(ILogger logger, string walletAddress);

        [LoggerMessage(EventId = 7402, Level = LogLevel.Debug, Message = "Validation cache hit: {CacheKey}")]
        public static partial void ValidationCacheHit(ILogger logger, string cacheKey);

        [LoggerMessage(EventId = 7403, Level = LogLevel.Information, Message = "Wallet validation completed: {WalletAddress}, valid: {IsValid}")]
        public static partial void WalletValidationCompleted(ILogger logger, string walletAddress, bool isValid);

        [LoggerMessage(EventId = 7404, Level = LogLevel.Information, Message = "Currency validation completed: {Currency}, valid: {IsValid}")]
        public static partial void CurrencyValidationCompleted(ILogger logger, string currency, bool isValid);

        [LoggerMessage(EventId = 7405, Level = LogLevel.Information, Message = "Amount validation completed: {Amount} {Currency}, valid: {IsValid}")]
        public static partial void AmountValidationCompleted(ILogger logger, decimal amount, string currency, bool isValid);

        [LoggerMessage(EventId = 7406, Level = LogLevel.Debug, Message = "Cache entries expired: {ExpiredCount}")]
        public static partial void CacheEntriesExpired(ILogger logger, int expiredCount);
    }
}

/// <summary>
/// Simple validation result for MVP
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    private ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Valid() => new(true);
    public static ValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}