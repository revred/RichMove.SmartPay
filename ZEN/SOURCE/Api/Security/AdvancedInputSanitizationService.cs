using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Api.Security;

public sealed partial class AdvancedInputSanitizationService : IHostedService, IDisposable
{
    private readonly ILogger<AdvancedInputSanitizationService> _logger;
    private readonly InputSanitizationOptions _options;
    private readonly Timer _maintenanceTimer;
    private readonly ConcurrentDictionary<string, SanitizationRule> _customRules;
    private readonly Counter<long> _sanitizationCount;
    private readonly Histogram<double> _sanitizationDuration;
    private readonly Counter<long> _threatDetectionCount;
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _ipThrottleCache;

    private static readonly Dictionary<string, Regex> CompiledPatterns = new()
    {
        ["xss"] = XssPattern(),
        ["sqli"] = SqlInjectionPattern(),
        ["xxe"] = XxePattern(),
        ["pathTraversal"] = PathTraversalPattern(),
        ["commandInjection"] = CommandInjectionPattern(),
        ["ldapInjection"] = LdapInjectionPattern(),
        ["scriptTag"] = ScriptTagPattern(),
        ["eventHandler"] = EventHandlerPattern(),
        ["javascript"] = JavaScriptUriPattern(),
        ["dataUri"] = DataUriPattern()
    };

    public AdvancedInputSanitizationService(
        ILogger<AdvancedInputSanitizationService> logger,
        IOptions<InputSanitizationOptions> options,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _options = options.Value;
        _customRules = new ConcurrentDictionary<string, SanitizationRule>();
        _ipThrottleCache = new ConcurrentDictionary<string, (int Count, DateTime WindowStart)>();

        var meter = meterFactory.Create("RichMove.SmartPay.InputSanitization");
        _sanitizationCount = meter.CreateCounter<long>("richmove_smartpay_sanitization_total");
        _sanitizationDuration = meter.CreateHistogram<double>("richmove_smartpay_sanitization_duration_seconds");
        _threatDetectionCount = meter.CreateCounter<long>("richmove_smartpay_threats_detected_total");

        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        InitializeDefaultRules();

        _logger.LogInformation("Advanced Input Sanitization Service initialized with {RuleCount} rules", _customRules.Count);
    }

    public async Task<SanitizationResult> SanitizeAsync(string input, SanitizationContext context)
    {
        using var activity = Activity.Current?.Source?.StartActivity("InputSanitization");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrEmpty(input))
            {
                return new SanitizationResult { SanitizedValue = input, IsClean = true };
            }

            var result = new SanitizationResult
            {
                OriginalValue = input,
                Context = context,
                ProcessedAt = DateTime.UtcNow
            };

            CheckRateLimit(context);

            var threats = await DetectThreatsAsync(input, context);
            result.DetectedThreats.AddRange(threats);

            var sanitized = ApplySanitizationAsync(input, context, threats);
            result.SanitizedValue = sanitized;
            result.IsClean = threats.Count == 0;
            result.WasModified = !string.Equals(input, sanitized, StringComparison.Ordinal);

            if (result.DetectedThreats.Any())
            {
                _threatDetectionCount.Add(result.DetectedThreats.Count,
                    new KeyValuePair<string, object?>("threat_types", string.Join(",", result.DetectedThreats.Select(t => t.Type))),
                    new KeyValuePair<string, object?>("context", context.Type.ToString()));

                _logger.LogWarning("Detected {ThreatCount} threats in input from {ClientIP}: {Threats}",
                    result.DetectedThreats.Count, context.ClientIP,
                    string.Join(", ", result.DetectedThreats.Select(t => t.Type)));
            }

            _sanitizationCount.Add(1,
                new KeyValuePair<string, object?>("context_type", context.Type.ToString()),
                new KeyValuePair<string, object?>("was_modified", result.WasModified));

            return result;
        }
        finally
        {
            _sanitizationDuration.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }

    public async Task<bool> ValidateInputAsync(string input, ValidationContext context)
    {
        var sanitizationContext = new SanitizationContext
        {
            Type = SanitizationType.Validation,
            FieldName = context.FieldName,
            ClientIP = context.ClientIP,
            UserAgent = context.UserAgent
        };

        var result = await SanitizeAsync(input, sanitizationContext);
        return result.IsClean && !result.WasModified;
    }

    private async Task<List<SecurityThreat>> DetectThreatsAsync(string input, SanitizationContext context)
    {
        var threats = new List<SecurityThreat>();

        foreach (var (patternName, regex) in CompiledPatterns)
        {
            if (regex.IsMatch(input))
            {
                var threat = new SecurityThreat
                {
                    Type = patternName,
                    Severity = GetThreatSeverity(patternName),
                    DetectedAt = DateTime.UtcNow,
                    Context = context.FieldName,
                    Pattern = regex.ToString()
                };

                threats.Add(threat);
            }
        }

        foreach (var rule in _customRules.Values.Where(r => r.IsEnabled))
        {
            if (await rule.MatchesAsync(input, context))
            {
                threats.Add(new SecurityThreat
                {
                    Type = rule.Name,
                    Severity = rule.Severity,
                    DetectedAt = DateTime.UtcNow,
                    Context = context.FieldName,
                    CustomRule = rule.Name
                });
            }
        }

        return threats;
    }

    private string ApplySanitizationAsync(string input, SanitizationContext context, List<SecurityThreat> threats)
    {
        var sanitized = input;

        switch (context.Type)
        {
            case SanitizationType.Html:
                sanitized = SanitizeHtml(sanitized);
                break;
            case SanitizationType.Sql:
                sanitized = SanitizeSql(sanitized);
                break;
            case SanitizationType.Script:
                sanitized = SanitizeScript(sanitized);
                break;
            case SanitizationType.Path:
                sanitized = SanitizePath(sanitized);
                break;
            case SanitizationType.Command:
                sanitized = SanitizeCommand(sanitized);
                break;
            default:
                sanitized = SanitizeGeneric(sanitized);
                break;
        }

        foreach (var threat in threats.Where(t => t.Severity >= ThreatSeverity.High))
        {
            sanitized = ApplyThreatSpecificSanitization(sanitized, threat);
        }

        return sanitized;
    }

    private string SanitizeHtml(string input)
    {
        var sanitized = HttpUtility.HtmlEncode(input);
        sanitized = CompiledPatterns["scriptTag"].Replace(sanitized, "");
        sanitized = CompiledPatterns["eventHandler"].Replace(sanitized, "");
        sanitized = CompiledPatterns["javascript"].Replace(sanitized, "");
        return sanitized;
    }

    private string SanitizeSql(string input)
    {
        var dangerous = new[] { "'", "\"", ";", "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "drop", "delete", "truncate", "alter" };
        var sanitized = input;

        foreach (var term in dangerous)
        {
            sanitized = sanitized.Replace(term, "", StringComparison.OrdinalIgnoreCase);
        }

        return sanitized.Trim();
    }

    private string SanitizeScript(string input)
    {
        var sanitized = input;
        sanitized = CompiledPatterns["scriptTag"].Replace(sanitized, "");
        sanitized = CompiledPatterns["javascript"].Replace(sanitized, "");
        sanitized = CompiledPatterns["eventHandler"].Replace(sanitized, "");

        var dangerous = new[] { "eval", "setTimeout", "setInterval", "Function", "document.write", "innerHTML", "outerHTML" };
        foreach (var term in dangerous)
        {
            sanitized = Regex.Replace(sanitized, Regex.Escape(term), "", RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    private string SanitizePath(string input)
    {
        var sanitized = input.Replace("..", "").Replace("\\", "/");
        sanitized = CompiledPatterns["pathTraversal"].Replace(sanitized, "");
        return Path.GetFileName(sanitized) ?? "";
    }

    private string SanitizeCommand(string input)
    {
        var dangerous = new[] { "|", "&", ";", "`", "$", "(", ")", "<", ">", ">>", "&&", "||" };
        var sanitized = input;

        foreach (var term in dangerous)
        {
            sanitized = sanitized.Replace(term, "");
        }

        return sanitized.Trim();
    }

    private string SanitizeGeneric(string input)
    {
        var sanitized = HttpUtility.HtmlEncode(input);
        sanitized = CompiledPatterns["scriptTag"].Replace(sanitized, "");
        return sanitized;
    }

    private string ApplyThreatSpecificSanitization(string input, SecurityThreat threat)
    {
        return threat.Type switch
        {
            "xss" => HttpUtility.HtmlEncode(input),
            "sqli" => SanitizeSql(input),
            "xxe" => Regex.Replace(input, @"<!(?:DOCTYPE|ENTITY)[^>]*>", "", RegexOptions.IgnoreCase),
            "pathTraversal" => SanitizePath(input),
            "commandInjection" => SanitizeCommand(input),
            "ldapInjection" => input.Replace("(", "").Replace(")", "").Replace("*", "").Replace("&", "").Replace("|", ""),
            _ => input
        };
    }

    private void CheckRateLimit(SanitizationContext context)
    {
        if (string.IsNullOrEmpty(context.ClientIP)) return;

        var key = context.ClientIP;
        var now = DateTime.UtcNow;
        var entry = _ipThrottleCache.AddOrUpdate(key,
            (1, now),
            (_, existing) =>
            {
                // Reset window if more than 1 minute old
                if (now - existing.WindowStart > TimeSpan.FromMinutes(1))
                    return (1, now);
                return (existing.Count + 1, existing.WindowStart);
            });

        if (entry.Count > _options.MaxRequestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded for IP {ClientIP}: {RequestCount} requests",
                context.ClientIP, entry.Count);

            throw new SecurityException($"Rate limit exceeded for IP {context.ClientIP}");
        }
    }

    private ThreatSeverity GetThreatSeverity(string threatType)
    {
        return threatType switch
        {
            "xss" => ThreatSeverity.Critical,
            "sqli" => ThreatSeverity.Critical,
            "xxe" => ThreatSeverity.High,
            "pathTraversal" => ThreatSeverity.High,
            "commandInjection" => ThreatSeverity.Critical,
            "ldapInjection" => ThreatSeverity.High,
            _ => ThreatSeverity.Medium
        };
    }

    private void InitializeDefaultRules()
    {
        _customRules.TryAdd("suspicious-file-extensions", new SanitizationRule
        {
            Name = "suspicious-file-extensions",
            Pattern = @"\.(exe|bat|cmd|scr|pif|com|vbs|js|jar)$",
            IsEnabled = true,
            Severity = ThreatSeverity.High,
            Description = "Detects potentially dangerous file extensions"
        });

        _customRules.TryAdd("credit-card-patterns", new SanitizationRule
        {
            Name = "credit-card-patterns",
            Pattern = @"\b(?:\d{4}[-\s]?){3}\d{4}\b",
            IsEnabled = true,
            Severity = ThreatSeverity.Critical,
            Description = "Detects credit card number patterns"
        });

        _customRules.TryAdd("ssn-patterns", new SanitizationRule
        {
            Name = "ssn-patterns",
            Pattern = @"\b\d{3}-\d{2}-\d{4}\b",
            IsEnabled = true,
            Severity = ThreatSeverity.High,
            Description = "Detects Social Security Number patterns"
        });
    }

    private void PerformMaintenance(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var expiredKeys = _ipThrottleCache
            .Where(kvp => kvp.Value.WindowStart < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _ipThrottleCache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Advanced Input Sanitization Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Advanced Input Sanitization Service stopping");
        _maintenanceTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _maintenanceTimer?.Dispose();
    }

    [GeneratedRegex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(@"on\w+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerPattern();

    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptUriPattern();

    [GeneratedRegex(@"data:(?:text\/html|application\/javascript)", RegexOptions.IgnoreCase)]
    private static partial Regex DataUriPattern();

    [GeneratedRegex(@"(?:'|""|;|--|\*|xp_|sp_|exec|drop|delete|insert|update|create|alter|truncate)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionPattern();

    [GeneratedRegex(@"<\?xml|<!DOCTYPE|<!ENTITY", RegexOptions.IgnoreCase)]
    private static partial Regex XxePattern();

    [GeneratedRegex(@"\.\.[/\\]|\.\.%2f|\.\.%5c", RegexOptions.IgnoreCase)]
    private static partial Regex PathTraversalPattern();

    [GeneratedRegex(@"[|&;`$(){}[\]<>]", RegexOptions.None)]
    private static partial Regex CommandInjectionPattern();

    [GeneratedRegex(@"[()&|*]", RegexOptions.None)]
    private static partial Regex LdapInjectionPattern();

    [GeneratedRegex(@"<[^>]+>|javascript:|data:|on\w+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex XssPattern();
}

public class InputSanitizationOptions
{
    public int MaxRequestsPerMinute { get; set; } = 100;
    public bool EnableCustomRules { get; set; } = true;
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public Dictionary<string, bool> EnabledSanitizers { get; set; } = new();
}

public class SanitizationResult
{
    public string OriginalValue { get; set; } = "";
    public string SanitizedValue { get; set; } = "";
    public bool IsClean { get; set; }
    public bool WasModified { get; set; }
    public List<SecurityThreat> DetectedThreats { get; set; } = new();
    public SanitizationContext Context { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

public class SanitizationContext
{
    public SanitizationType Type { get; set; }
    public string FieldName { get; set; } = "";
    public string ClientIP { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class ValidationContext
{
    public string FieldName { get; set; } = "";
    public string ClientIP { get; set; } = "";
    public string UserAgent { get; set; } = "";
}

public class SecurityThreat
{
    public string Type { get; set; } = "";
    public ThreatSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Context { get; set; } = "";
    public string Pattern { get; set; } = "";
    public string? CustomRule { get; set; }
}

public class SanitizationRule
{
    public string Name { get; set; } = "";
    public string Pattern { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public ThreatSeverity Severity { get; set; } = ThreatSeverity.Medium;
    public string Description { get; set; } = "";
    private Regex? _compiledPattern;

    public Task<bool> MatchesAsync(string input, SanitizationContext context)
    {
        _compiledPattern ??= new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return Task.FromResult(_compiledPattern.IsMatch(input));
    }
}

public enum SanitizationType
{
    Generic,
    Html,
    Sql,
    Script,
    Path,
    Command,
    Validation
}

