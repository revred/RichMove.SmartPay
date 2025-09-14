using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RichMove.SmartPay.Api.Validation;

/// <summary>
/// FluentValidation integration with async database validation and caching
/// Provides enterprise-grade validation with localization support
/// </summary>
public static partial class FluentValidationExtensions
{
    private static readonly ConcurrentDictionary<Type, IValidator> _validatorCache = new();

    /// <summary>
    /// Add FluentValidation with SmartPay optimizations
    /// </summary>
    public static IServiceCollection AddSmartPayValidation(this IServiceCollection services)
    {
        // Register core validation services
        services.AddValidatorsFromAssemblyContaining<FluentValidationExtensions>();
        services.AddSingleton<ValidationResultCache>();
        services.AddSingleton<DataSanitizer>();
        services.AddScoped<AsyncValidationService>();

        // Register custom validators
        services.AddTransient<IValidator<PaymentRequest>, PaymentRequestValidator>();
        services.AddTransient<IValidator<TransferRequest>, TransferRequestValidator>();
        services.AddTransient<IValidator<WalletCreateRequest>, WalletCreateRequestValidator>();

        Log.ValidationServicesRegistered(CreateLogger(), services.Count);
        return services;
    }

    /// <summary>
    /// Validate with caching and async support
    /// </summary>
    public static async Task<SmartPayValidationResult> ValidateWithCacheAsync<T>(
        this IValidator<T> validator,
        T instance,
        ValidationResultCache cache,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(cache);

        var cacheKey = GenerateCacheKey(typeof(T), instance);

        // Try cache first
        if (cache.TryGet(cacheKey, out var cachedResult))
        {
            Log.ValidationCacheHit(CreateLogger(), typeof(T).Name, cacheKey);
            return cachedResult;
        }

        // Perform validation
        var validationResult = await validator.ValidateAsync(instance, cancellationToken);
        var smartPayResult = ConvertToSmartPayResult(validationResult);

        // Cache successful validations for longer
        var cacheTime = smartPayResult.IsValid
            ? TimeSpan.FromMinutes(15)
            : TimeSpan.FromMinutes(2);

        cache.Set(cacheKey, smartPayResult, cacheTime);

        Log.ValidationCompleted(CreateLogger(), typeof(T).Name, smartPayResult.IsValid, smartPayResult.Errors.Count);
        return smartPayResult;
    }

    private static string GenerateCacheKey<T>(Type type, T instance)
    {
        var hash = instance!.GetHashCode();
        return $"validation:{type.Name}:{hash:X}";
    }

    private static SmartPayValidationResult ConvertToSmartPayResult(ValidationResult fluentResult)
    {
        return new SmartPayValidationResult
        {
            IsValid = fluentResult.IsValid,
            Errors = fluentResult.Errors.Select(e => new SmartPayValidationError
            {
                PropertyName = e.PropertyName,
                ErrorMessage = e.ErrorMessage,
                ErrorCode = e.ErrorCode,
                AttemptedValue = e.AttemptedValue,
                Severity = ConvertSeverity(e.Severity)
            }).ToList()
        };
    }

    private static SmartPaySeverity ConvertSeverity(Severity fluentSeverity)
    {
        return fluentSeverity switch
        {
            Severity.Error => SmartPaySeverity.Error,
            Severity.Warning => SmartPaySeverity.Warning,
            Severity.Info => SmartPaySeverity.Info,
            _ => SmartPaySeverity.Error
        };
    }

    private static ILogger CreateLogger() =>
        LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("SmartPayValidation");

    private static partial class Log
    {
        [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Validation services registered: {ServiceCount} total services")]
        public static partial void ValidationServicesRegistered(ILogger logger, int serviceCount);

        [LoggerMessage(EventId = 7002, Level = LogLevel.Debug, Message = "Validation cache hit for {TypeName}, key: {CacheKey}")]
        public static partial void ValidationCacheHit(ILogger logger, string typeName, string cacheKey);

        [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "Validation completed for {TypeName}: Valid={IsValid}, Errors={ErrorCount}")]
        public static partial void ValidationCompleted(ILogger logger, string typeName, bool isValid, int errorCount);
    }
}

/// <summary>
/// SmartPay validation result with enhanced metadata
/// </summary>
public sealed class SmartPayValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<SmartPayValidationError> Errors { get; init; } = Array.Empty<SmartPayValidationError>();
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
    public string? ValidationId { get; init; } = Guid.NewGuid().ToString("N")[..8];
}

/// <summary>
/// Enhanced validation error with additional context
/// </summary>
public sealed class SmartPayValidationError
{
    public required string PropertyName { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public object? AttemptedValue { get; init; }
    public SmartPaySeverity Severity { get; init; } = SmartPaySeverity.Error;
}

/// <summary>
/// Validation error severity levels
/// </summary>
public enum SmartPaySeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Payment request validator with business rules
/// </summary>
public sealed partial class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    private static readonly Regex CurrencyCodeRegex = CurrencyCodeRegexCompiled();

    public PaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be positive")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount exceeds maximum limit");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Matches(CurrencyCodeRegex).WithMessage("Invalid currency code format")
            .Length(3).WithMessage("Currency must be 3 characters");

        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .EmailAddress().When(x => x.Recipient?.Contains('@') == true)
            .WithMessage("Invalid email format");

        RuleFor(x => x.Reference)
            .MaximumLength(100).WithMessage("Reference too long")
            .Must(BeValidReference).WithMessage("Reference contains invalid characters");
    }

    private static bool BeValidReference(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return true;
        return reference.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || "-_".Contains(c));
    }

    [GeneratedRegex(@"^[A-Z]{3}$", RegexOptions.Compiled)]
    private static partial Regex CurrencyCodeRegexCompiled();
}

/// <summary>
/// Transfer request validator with enhanced validation
/// </summary>
public sealed class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.FromWallet)
            .NotEmpty().WithMessage("Source wallet is required")
            .Matches(@"^0x[a-fA-F0-9]{40}$").WithMessage("Invalid wallet address format");

        RuleFor(x => x.ToWallet)
            .NotEmpty().WithMessage("Destination wallet is required")
            .Matches(@"^0x[a-fA-F0-9]{40}$").WithMessage("Invalid wallet address format");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer amount must be positive");

        RuleFor(x => x)
            .Must(x => x.FromWallet != x.ToWallet)
            .WithMessage("Source and destination wallets cannot be the same");
    }
}

/// <summary>
/// Wallet creation validator with security checks
/// </summary>
public sealed class WalletCreateRequestValidator : AbstractValidator<WalletCreateRequest>
{
    public WalletCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Wallet name is required")
            .Length(2, 50).WithMessage("Wallet name must be 2-50 characters")
            .Must(BeValidWalletName).WithMessage("Wallet name contains invalid characters");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Must(BeSupportedCurrency).WithMessage("Unsupported currency");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative");
    }

    private static bool BeValidWalletName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || "-_".Contains(c));
    }

    private static bool BeSupportedCurrency(string? currency)
    {
        var supportedCurrencies = new[] { "USD", "EUR", "GBP", "BTC", "ETH" };
        return !string.IsNullOrEmpty(currency) && supportedCurrencies.Contains(currency.ToUpperInvariant());
    }
}

// Placeholder request models for validation
public record PaymentRequest(decimal Amount, string Currency, string Recipient, string? Reference);
public record TransferRequest(string FromWallet, string ToWallet, decimal Amount);
public record WalletCreateRequest(string Name, string Currency, decimal InitialBalance);