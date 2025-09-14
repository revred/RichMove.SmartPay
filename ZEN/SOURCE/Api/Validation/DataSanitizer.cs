using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RichMove.SmartPay.Api.Validation;

/// <summary>
/// Data sanitization pipeline with XSS prevention and SQL injection protection
/// Provides comprehensive input sanitization for security hardening
/// </summary>
public sealed partial class DataSanitizer
{
    private readonly ILogger<DataSanitizer> _logger;
    private readonly DataSanitizerOptions _options;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly JavaScriptEncoder _jsEncoder;
    private readonly UrlEncoder _urlEncoder;
    private readonly ConcurrentDictionary<string, SanitizationResult> _patternCache;

    // Pre-compiled regex patterns for performance
    private static readonly Regex SqlInjectionPattern = SqlInjectionRegex();
    private static readonly Regex XssPattern = XssRegex();
    private static readonly Regex ScriptPattern = ScriptTagRegex();
    private static readonly Regex HtmlPattern = HtmlTagRegex();

    public DataSanitizer(ILogger<DataSanitizer> logger, IOptions<DataSanitizerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
        _htmlEncoder = HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement);
        _jsEncoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin);
        _urlEncoder = UrlEncoder.Create(UnicodeRanges.BasicLatin);
        _patternCache = new ConcurrentDictionary<string, SanitizationResult>();

        Log.SanitizerInitialized(_logger, _options.MaxInputLength, _options.EnablePatternCaching);
    }

    /// <summary>
    /// Sanitize input string with comprehensive security checks
    /// </summary>
    public SanitizationResult Sanitize(string? input, SanitizationType type = SanitizationType.General)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new SanitizationResult
            {
                SanitizedValue = input ?? string.Empty,
                IsModified = false,
                ThreatLevel = ThreatLevel.None,
                DetectedThreats = Array.Empty<string>()
            };
        }

        // Check length limits
        if (input.Length > _options.MaxInputLength)
        {
            Log.InputTooLong(_logger, input.Length, _options.MaxInputLength);
            return CreateThreatResult(input, "Input exceeds maximum length", ThreatLevel.High);
        }

        // Check cache if enabled
        if (_options.EnablePatternCaching)
        {
            var cacheKey = GenerateCacheKey(input, type);
            if (_patternCache.TryGetValue(cacheKey, out var cachedResult))
            {
                Log.SanitizationCacheHit(_logger, cacheKey);
                return cachedResult;
            }
        }

        var result = PerformSanitization(input, type);

        // Cache result if enabled
        if (_options.EnablePatternCaching && _patternCache.Count < _options.MaxCacheEntries)
        {
            var cacheKey = GenerateCacheKey(input, type);
            _patternCache.TryAdd(cacheKey, result);
        }

        Log.SanitizationCompleted(_logger, type.ToString(), result.IsModified, result.ThreatLevel.ToString(), result.DetectedThreats.Count);
        return result;
    }

    /// <summary>
    /// Sanitize JSON input with structure validation
    /// </summary>
    public SanitizationResult SanitizeJson(string? jsonInput)
    {
        if (string.IsNullOrEmpty(jsonInput))
        {
            return CreateSafeResult(jsonInput ?? string.Empty);
        }

        try
        {
            // Parse and re-serialize to normalize
            using var document = JsonDocument.Parse(jsonInput);
            var normalizedJson = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Check for suspicious patterns in JSON
            var threats = new List<string>();
            if (XssPattern.IsMatch(normalizedJson)) threats.Add("XSS pattern detected");
            if (ScriptPattern.IsMatch(normalizedJson)) threats.Add("Script injection detected");

            var threatLevel = threats.Count > 0 ? ThreatLevel.Medium : ThreatLevel.None;

            return new SanitizationResult
            {
                SanitizedValue = normalizedJson,
                IsModified = !string.Equals(jsonInput, normalizedJson, StringComparison.Ordinal),
                ThreatLevel = threatLevel,
                DetectedThreats = threats
            };
        }
        catch (JsonException ex)
        {
            Log.InvalidJsonInput(_logger, ex);
            return CreateThreatResult(jsonInput, "Invalid JSON format", ThreatLevel.High);
        }
    }

    /// <summary>
    /// Sanitize SQL parameters to prevent injection
    /// </summary>
    public SanitizationResult SanitizeSqlParameter(string? parameter)
    {
        if (string.IsNullOrEmpty(parameter))
        {
            return CreateSafeResult(parameter ?? string.Empty);
        }

        var threats = new List<string>();
        var sanitized = parameter;

        // Check for SQL injection patterns
        if (SqlInjectionPattern.IsMatch(parameter))
        {
            threats.Add("SQL injection pattern detected");
            // Remove dangerous SQL keywords and characters
            sanitized = RemoveSqlThreats(parameter);
        }

        // Encode for SQL safety
        sanitized = sanitized.Replace("'", "''"); // Escape single quotes

        return new SanitizationResult
        {
            SanitizedValue = sanitized,
            IsModified = !string.Equals(parameter, sanitized, StringComparison.Ordinal),
            ThreatLevel = threats.Count > 0 ? ThreatLevel.High : ThreatLevel.None,
            DetectedThreats = threats
        };
    }

    private SanitizationResult PerformSanitization(string input, SanitizationType type)
    {
        var threats = new List<string>();
        var sanitized = input;
        var isModified = false;

        // XSS detection and prevention
        if (XssPattern.IsMatch(input))
        {
            threats.Add("XSS pattern detected");
            sanitized = _htmlEncoder.Encode(sanitized);
            isModified = true;
        }

        // Script tag detection
        if (ScriptPattern.IsMatch(input))
        {
            threats.Add("Script tag detected");
            sanitized = ScriptPattern.Replace(sanitized, "");
            isModified = true;
        }

        // SQL injection detection
        if (SqlInjectionPattern.IsMatch(input))
        {
            threats.Add("SQL injection pattern detected");
            sanitized = RemoveSqlThreats(sanitized);
            isModified = true;
        }

        // Type-specific sanitization
        switch (type)
        {
            case SanitizationType.Html:
                if (HtmlPattern.IsMatch(sanitized))
                {
                    sanitized = _htmlEncoder.Encode(sanitized);
                    isModified = true;
                }
                break;

            case SanitizationType.JavaScript:
                sanitized = _jsEncoder.Encode(sanitized);
                isModified = true;
                break;

            case SanitizationType.Url:
                sanitized = _urlEncoder.Encode(sanitized);
                isModified = true;
                break;

            case SanitizationType.Email:
                sanitized = SanitizeEmail(sanitized);
                isModified = !string.Equals(input, sanitized, StringComparison.Ordinal);
                break;
        }

        var threatLevel = DetermineThreatLevel(threats.Count, isModified);

        return new SanitizationResult
        {
            SanitizedValue = sanitized,
            IsModified = isModified,
            ThreatLevel = threatLevel,
            DetectedThreats = threats
        };
    }

    private static string RemoveSqlThreats(string input)
    {
        // Remove common SQL injection patterns
        var dangerous = new[] { "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "drop", "truncate", "delete", "insert", "update" };
        var result = input;

        foreach (var keyword in dangerous)
        {
            result = result.Replace(keyword, "", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string SanitizeEmail(string email)
    {
        // Basic email sanitization - remove dangerous characters
        return email.Where(c => char.IsLetterOrDigit(c) || "@.-_".Contains(c)).Aggregate(new StringBuilder(), (sb, c) => sb.Append(c)).ToString();
    }

    private static ThreatLevel DetermineThreatLevel(int threatCount, bool isModified)
    {
        return threatCount switch
        {
            0 when !isModified => ThreatLevel.None,
            0 when isModified => ThreatLevel.Low,
            1 => ThreatLevel.Medium,
            _ => ThreatLevel.High
        };
    }

    private static SanitizationResult CreateSafeResult(string input)
    {
        return new SanitizationResult
        {
            SanitizedValue = input,
            IsModified = false,
            ThreatLevel = ThreatLevel.None,
            DetectedThreats = Array.Empty<string>()
        };
    }

    private static SanitizationResult CreateThreatResult(string input, string threat, ThreatLevel level)
    {
        return new SanitizationResult
        {
            SanitizedValue = string.Empty, // Block dangerous input
            IsModified = true,
            ThreatLevel = level,
            DetectedThreats = new[] { threat }
        };
    }

    private static string GenerateCacheKey(string input, SanitizationType type)
    {
        return $"{type}:{input.GetHashCode():X}";
    }

    // Compiled regex patterns for performance
    [GeneratedRegex(@"(\b(exec|execute|drop|truncate|delete|insert|update|select|union|script|javascript|vbscript|onload|onerror)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SqlInjectionRegex();

    [GeneratedRegex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex XssRegex();

    [GeneratedRegex(@"<script\b[^>]*>(.*?)<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    private static partial class Log
    {
        [LoggerMessage(EventId = 7201, Level = LogLevel.Information, Message = "Data sanitizer initialized: max length {MaxLength}, caching enabled: {CachingEnabled}")]
        public static partial void SanitizerInitialized(ILogger logger, int maxLength, bool cachingEnabled);

        [LoggerMessage(EventId = 7202, Level = LogLevel.Warning, Message = "Input too long: {ActualLength} exceeds maximum {MaxLength}")]
        public static partial void InputTooLong(ILogger logger, int actualLength, int maxLength);

        [LoggerMessage(EventId = 7203, Level = LogLevel.Debug, Message = "Sanitization cache hit: {CacheKey}")]
        public static partial void SanitizationCacheHit(ILogger logger, string cacheKey);

        [LoggerMessage(EventId = 7204, Level = LogLevel.Information, Message = "Sanitization completed: type {Type}, modified {IsModified}, threat level {ThreatLevel}, threats {ThreatCount}")]
        public static partial void SanitizationCompleted(ILogger logger, string type, bool isModified, string threatLevel, int threatCount);

        [LoggerMessage(EventId = 7205, Level = LogLevel.Warning, Message = "Invalid JSON input detected")]
        public static partial void InvalidJsonInput(ILogger logger, Exception exception);
    }
}

/// <summary>
/// Sanitization result with threat assessment
/// </summary>
public sealed class SanitizationResult
{
    public required string SanitizedValue { get; init; }
    public required bool IsModified { get; init; }
    public required ThreatLevel ThreatLevel { get; init; }
    public required IReadOnlyList<string> DetectedThreats { get; init; }
}

/// <summary>
/// Sanitization type for different contexts
/// </summary>
public enum SanitizationType
{
    General,
    Html,
    JavaScript,
    Url,
    Email,
    SqlParameter
}

/// <summary>
/// Security threat level assessment
/// </summary>
public enum ThreatLevel
{
    None,
    Low,
    Medium,
    High
}

/// <summary>
/// Data sanitizer configuration options
/// </summary>
public sealed class DataSanitizerOptions
{
    public int MaxInputLength { get; set; } = 50_000;
    public bool EnablePatternCaching { get; set; } = true;
    public int MaxCacheEntries { get; set; } = 1_000;
    public bool StrictMode { get; set; } = true;
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromHours(1);
}