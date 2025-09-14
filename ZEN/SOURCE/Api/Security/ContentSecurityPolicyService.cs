using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Api.Security;

public sealed class ContentSecurityPolicyService : IHostedService, IDisposable
{
    private readonly ILogger<ContentSecurityPolicyService> _logger;
    private readonly ContentSecurityPolicyOptions _options;
    private readonly Timer _reportProcessingTimer;
    private readonly ConcurrentQueue<CspViolationReport> _violationQueue;
    private readonly ConcurrentDictionary<string, DateTime> _nonceCache;
    private readonly Counter<long> _violationCount;
    private readonly Counter<long> _nonceGenerated;
    private readonly Histogram<double> _policyGenerationTime;

    private static readonly Dictionary<string, List<string>> DefaultDirectives = new()
    {
        ["default-src"] = new() { "'self'" },
        ["script-src"] = new() { "'self'", "'unsafe-inline'" },
        ["style-src"] = new() { "'self'", "'unsafe-inline'" },
        ["img-src"] = new() { "'self'", "data:", "https:" },
        ["font-src"] = new() { "'self'" },
        ["connect-src"] = new() { "'self'" },
        ["frame-ancestors"] = new() { "'none'" },
        ["form-action"] = new() { "'self'" },
        ["base-uri"] = new() { "'self'" },
        ["object-src"] = new() { "'none'" },
        ["media-src"] = new() { "'self'" },
        ["worker-src"] = new() { "'self'" },
        ["manifest-src"] = new() { "'self'" }
    };

    public ContentSecurityPolicyService(
        ILogger<ContentSecurityPolicyService> logger,
        IOptions<ContentSecurityPolicyOptions> options,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _options = options.Value;
        _violationQueue = new ConcurrentQueue<CspViolationReport>();
        _nonceCache = new ConcurrentDictionary<string, DateTime>();

        var meter = meterFactory.Create("RichMove.SmartPay.ContentSecurityPolicy");
        _violationCount = meter.CreateCounter<long>("richmove_smartpay_csp_violations_total");
        _nonceGenerated = meter.CreateCounter<long>("richmove_smartpay_csp_nonces_generated_total");
        _policyGenerationTime = meter.CreateHistogram<double>("richmove_smartpay_csp_policy_generation_duration_seconds");

        _reportProcessingTimer = new Timer(ProcessViolationReports, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.LogInformation("Content Security Policy Service initialized with {DirectiveCount} directives", _options.Directives.Count);
    }

    public string GeneratePolicy(HttpContext context, CspMode mode = CspMode.Enforce)
    {
        using var activity = Activity.Current?.Source?.StartActivity("GenerateCSPPolicy");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var directives = GetEffectiveDirectives(context);
            var nonce = GenerateNonce(context);

            if (!string.IsNullOrEmpty(nonce))
            {
                UpdateDirectivesWithNonce(directives, nonce);
            }

            var policy = BuildPolicyString(directives);

            if (_options.EnableReporting && !string.IsNullOrEmpty(_options.ReportUri))
            {
                policy += $"; report-uri {_options.ReportUri}";
            }

            if (_options.EnableReportTo && !string.IsNullOrEmpty(_options.ReportToGroup))
            {
                policy += $"; report-to {_options.ReportToGroup}";
            }

            _logger.LogDebug("Generated CSP policy for {Path}: {Policy}", context.Request.Path, policy);

            return policy;
        }
        finally
        {
            _policyGenerationTime.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }

    public string GenerateNonce(HttpContext context)
    {
        if (!_options.EnableNonces) return "";

        var nonce = GenerateSecureNonce();
        var expiry = DateTime.UtcNow.AddMinutes(_options.NonceExpiryMinutes);

        _nonceCache.TryAdd(nonce, expiry);
        context.Items["csp-nonce"] = nonce;

        _nonceGenerated.Add(1,
            new KeyValuePair<string, object?>("path", context.Request.Path.ToString()));

        return nonce;
    }

    public void ProcessViolationReportAsync(CspViolationReport report)
    {
        _violationQueue.Enqueue(report);

        _violationCount.Add(1,
            new KeyValuePair<string, object?>("directive", report.ViolatedDirective),
            new KeyValuePair<string, object?>("blocked_uri", report.BlockedUri),
            new KeyValuePair<string, object?>("source_file", report.SourceFile ?? "unknown"));

        _logger.LogWarning("CSP Violation: {Directive} blocked {Uri} from {Source}",
            report.ViolatedDirective, report.BlockedUri, report.SourceFile ?? "unknown");

        if (_options.AutoUpdatePolicy)
        {
            UpdatePolicyFromViolation(report);
        }
    }

    public Dictionary<string, List<string>> GetEffectiveDirectives(HttpContext context)
    {
        var directives = new Dictionary<string, List<string>>();

        // Start with default directives
        foreach (var (key, value) in DefaultDirectives)
        {
            directives[key] = new List<string>(value);
        }

        // Apply configured directives
        foreach (var (key, value) in _options.Directives)
        {
            if (value.Any())
            {
                directives[key] = new List<string>(value);
            }
        }

        // Apply context-specific overrides
        ApplyContextSpecificDirectives(directives, context);

        // Apply environment-specific overrides
        if (_options.DevelopmentOverrides.Any() && IsDevelopmentEnvironment())
        {
            foreach (var (key, value) in _options.DevelopmentOverrides)
            {
                if (value.Any())
                {
                    directives[key] = new List<string>(value);
                }
            }
        }

        return directives;
    }

    public bool ValidateNonce(string nonce)
    {
        if (string.IsNullOrEmpty(nonce)) return false;

        if (_nonceCache.TryGetValue(nonce, out var expiry))
        {
            if (DateTime.UtcNow <= expiry)
            {
                return true;
            }

            _nonceCache.TryRemove(nonce, out _);
        }

        return false;
    }

    private void UpdateDirectivesWithNonce(Dictionary<string, List<string>> directives, string nonce)
    {
        var nonceValue = $"'nonce-{nonce}'";

        if (directives.ContainsKey("script-src"))
        {
            if (!directives["script-src"].Contains(nonceValue))
            {
                directives["script-src"].Add(nonceValue);
            }

            // Remove unsafe-inline when nonce is present for better security
            if (_options.RemoveUnsafeInlineWithNonce)
            {
                directives["script-src"].Remove("'unsafe-inline'");
            }
        }

        if (directives.ContainsKey("style-src"))
        {
            if (!directives["style-src"].Contains(nonceValue))
            {
                directives["style-src"].Add(nonceValue);
            }

            if (_options.RemoveUnsafeInlineWithNonce)
            {
                directives["style-src"].Remove("'unsafe-inline'");
            }
        }
    }

    private string BuildPolicyString(Dictionary<string, List<string>> directives)
    {
        var policyParts = new List<string>();

        foreach (var (directive, sources) in directives.OrderBy(d => d.Key))
        {
            if (sources.Any())
            {
                policyParts.Add($"{directive} {string.Join(" ", sources)}");
            }
        }

        return string.Join("; ", policyParts);
    }

    private void ApplyContextSpecificDirectives(Dictionary<string, List<string>> directives, HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        if (path.Contains("/api/"))
        {
            // API endpoints typically don't need script execution
            directives["script-src"] = new List<string> { "'none'" };
            directives["object-src"] = new List<string> { "'none'" };
        }

        if (path.Contains("/admin/"))
        {
            // Admin pages may need stricter policies
            directives["frame-ancestors"] = new List<string> { "'none'" };
            directives["form-action"] = new List<string> { "'self'" };
        }

        if (path.Contains("/public/") || path.Contains("/static/"))
        {
            // Public/static content may have different requirements
            if (!directives.ContainsKey("img-src"))
            {
                directives["img-src"] = new List<string>();
            }
            if (!directives["img-src"].Contains("*"))
            {
                directives["img-src"].Add("https:");
            }
        }

        // Apply user-defined path-specific overrides
        foreach (var pathOverride in _options.PathSpecificOverrides)
        {
            if (path.Contains(pathOverride.Key.ToLowerInvariant()))
            {
                foreach (var (directive, sources) in pathOverride.Value)
                {
                    directives[directive] = new List<string>(sources);
                }
            }
        }
    }

    private void UpdatePolicyFromViolation(CspViolationReport report)
    {
        if (string.IsNullOrEmpty(report.ViolatedDirective) || string.IsNullOrEmpty(report.BlockedUri))
            return;

        var directive = report.ViolatedDirective;
        var uri = report.BlockedUri;

        // Only auto-update for certain safe scenarios
        if (IsAutoUpdateSafe(directive, uri))
        {
            if (_options.Directives.ContainsKey(directive))
            {
                var currentSources = _options.Directives[directive];
                if (!currentSources.Contains(uri))
                {
                    var newSources = new List<string>(currentSources) { uri };
                    _options.Directives[directive] = newSources;

                    _logger.LogInformation("Auto-updated CSP directive {Directive} to include {Uri}",
                        directive, uri);
                }
            }
        }
    }

    private bool IsAutoUpdateSafe(string directive, string uri)
    {
        // Only allow auto-update for specific scenarios
        var safeDirectives = new[] { "img-src", "font-src", "connect-src" };

        if (!safeDirectives.Contains(directive))
            return false;

        // Only allow HTTPS URIs
        if (!uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        // Block dangerous domains
        var dangerousDomains = new[] { "javascript:", "data:", "blob:", "filesystem:" };
        if (dangerousDomains.Any(d => uri.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static string GenerateSecureNonce()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    private bool IsDevelopmentEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ProcessViolationReports(object? state)
    {
        var processedCount = 0;
        var reports = new List<CspViolationReport>();

        while (_violationQueue.TryDequeue(out var report) && processedCount < _options.MaxReportsPerBatch)
        {
            reports.Add(report);
            processedCount++;
        }

        if (reports.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessViolationReportsBatch(reports);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process CSP violation reports batch");
                }
            });
        }

        CleanupExpiredNonces();
    }

    private async Task ProcessViolationReportsBatch(List<CspViolationReport> reports)
    {
        if (_options.EnableViolationAnalytics)
        {
            var analytics = AnalyzeViolations(reports);
            _logger.LogInformation("CSP Violation Analytics: {Analytics}", JsonSerializer.Serialize(analytics));
        }

        if (!string.IsNullOrEmpty(_options.ViolationWebhookUrl))
        {
            await SendViolationsToWebhook(reports);
        }
    }

    private CspViolationAnalytics AnalyzeViolations(List<CspViolationReport> reports)
    {
        return new CspViolationAnalytics
        {
            TotalViolations = reports.Count,
            ViolationsByDirective = reports.GroupBy(r => r.ViolatedDirective)
                .ToDictionary(g => g.Key, g => g.Count()),
            ViolationsBySource = reports.GroupBy(r => r.SourceFile ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count()),
            ViolationsByUri = reports.GroupBy(r => r.BlockedUri)
                .ToDictionary(g => g.Key, g => g.Count()),
            TimeRange = new
            {
                Start = reports.Min(r => r.Timestamp),
                End = reports.Max(r => r.Timestamp)
            }
        };
    }

    private async Task SendViolationsToWebhook(List<CspViolationReport> reports)
    {
        try
        {
            using var httpClient = new HttpClient();
            var payload = JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow,
                violation_count = reports.Count,
                violations = reports.Take(10) // Limit to prevent payload bloat
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await httpClient.PostAsync(_options.ViolationWebhookUrl, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send CSP violations to webhook");
        }
    }

    private void CleanupExpiredNonces()
    {
        var cutoff = DateTime.UtcNow;
        var expiredNonces = _nonceCache
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var nonce in expiredNonces)
        {
            _nonceCache.TryRemove(nonce, out _);
        }

        if (expiredNonces.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired nonces", expiredNonces.Count);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Content Security Policy Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Content Security Policy Service stopping");
        _reportProcessingTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _reportProcessingTimer?.Dispose();
    }
}

public class ContentSecurityPolicyOptions
{
    public Dictionary<string, List<string>> Directives { get; set; } = new();
    public Dictionary<string, List<string>> DevelopmentOverrides { get; set; } = new();
    public Dictionary<string, Dictionary<string, List<string>>> PathSpecificOverrides { get; set; } = new();

    public bool EnableNonces { get; set; } = true;
    public int NonceExpiryMinutes { get; set; } = 15;
    public bool RemoveUnsafeInlineWithNonce { get; set; } = true;

    public bool EnableReporting { get; set; } = true;
    public string ReportUri { get; set; } = "/api/csp/violations";
    public bool EnableReportTo { get; set; } = false;
    public string ReportToGroup { get; set; } = "csp-endpoint";

    public bool AutoUpdatePolicy { get; set; } = false;
    public bool EnableViolationAnalytics { get; set; } = true;
    public string? ViolationWebhookUrl { get; set; }
    public int MaxReportsPerBatch { get; set; } = 100;
}

public class CspViolationReport
{
    public string DocumentUri { get; set; } = "";
    public string Referrer { get; set; } = "";
    public string ViolatedDirective { get; set; } = "";
    public string EffectiveDirective { get; set; } = "";
    public string OriginalPolicy { get; set; } = "";
    public string BlockedUri { get; set; } = "";
    public string? SourceFile { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public string StatusCode { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CspViolationAnalytics
{
    public int TotalViolations { get; set; }
    public Dictionary<string, int> ViolationsByDirective { get; set; } = new();
    public Dictionary<string, int> ViolationsBySource { get; set; } = new();
    public Dictionary<string, int> ViolationsByUri { get; set; } = new();
    public object? TimeRange { get; set; }
}

public enum CspMode
{
    Enforce,
    ReportOnly
}