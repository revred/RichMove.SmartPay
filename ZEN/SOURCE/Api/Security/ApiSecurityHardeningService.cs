using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Api.Security;

public sealed class ApiSecurityHardeningService : IHostedService, IDisposable
{
    private readonly ILogger<ApiSecurityHardeningService> _logger;
    private readonly ApiSecurityHardeningOptions _options;
    private readonly Timer _maintenanceTimer;
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimitCache;
    private readonly ConcurrentDictionary<string, ApiKeyInfo> _apiKeyCache;
    private readonly ConcurrentDictionary<string, ApiSecurityEvent> _securityEvents;
    private readonly Counter<long> _requestCount;
    private readonly Counter<long> _blockedRequestCount;
    private readonly Histogram<double> _securityCheckDuration;
    private readonly Counter<long> _apiKeyValidationCount;

    public ApiSecurityHardeningService(
        ILogger<ApiSecurityHardeningService> logger,
        IOptions<ApiSecurityHardeningOptions> options,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _options = options.Value;
        _rateLimitCache = new ConcurrentDictionary<string, RateLimitState>();
        _apiKeyCache = new ConcurrentDictionary<string, ApiKeyInfo>();
        _securityEvents = new ConcurrentDictionary<string, ApiSecurityEvent>();

        var meter = meterFactory.Create("RichMove.SmartPay.ApiSecurity");
        _requestCount = meter.CreateCounter<long>("richmove_smartpay_api_requests_total");
        _blockedRequestCount = meter.CreateCounter<long>("richmove_smartpay_api_requests_blocked_total");
        _securityCheckDuration = meter.CreateHistogram<double>("richmove_smartpay_api_security_check_duration_seconds");
        _apiKeyValidationCount = meter.CreateCounter<long>("richmove_smartpay_api_key_validations_total");

        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("API Security Hardening Service initialized");
    }

    public async Task<SecurityCheckResult> ValidateRequestAsync(HttpContext context)
    {
        using var activity = Activity.StartActivity("ApiSecurityValidation");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = new SecurityCheckResult { IsAllowed = true };

            _requestCount.Add(1,
                new KeyValuePair<string, object?>("endpoint", context.Request.Path.ToString()),
                new KeyValuePair<string, object?>("method", context.Request.Method));

            // IP-based security checks
            var ipCheck = await ValidateIpAddressAsync(context);
            if (!ipCheck.IsAllowed)
            {
                result.IsAllowed = false;
                result.Reasons.AddRange(ipCheck.Reasons);
                result.SecurityLevel = SecurityLevel.Blocked;
            }

            // Rate limiting
            if (result.IsAllowed && _options.EnableRateLimiting)
            {
                var rateLimitCheck = await CheckRateLimitAsync(context);
                if (!rateLimitCheck.IsAllowed)
                {
                    result.IsAllowed = false;
                    result.Reasons.AddRange(rateLimitCheck.Reasons);
                    result.SecurityLevel = SecurityLevel.RateLimited;
                }
            }

            // API Key validation
            if (result.IsAllowed && _options.RequireApiKey)
            {
                var apiKeyCheck = await ValidateApiKeyAsync(context);
                if (!apiKeyCheck.IsAllowed)
                {
                    result.IsAllowed = false;
                    result.Reasons.AddRange(apiKeyCheck.Reasons);
                    result.SecurityLevel = SecurityLevel.Unauthorized;
                }
                else
                {
                    result.ApiKeyInfo = apiKeyCheck.ApiKeyInfo;
                }
            }

            // Request signature validation
            if (result.IsAllowed && _options.RequireRequestSignature)
            {
                var signatureCheck = await ValidateRequestSignatureAsync(context);
                if (!signatureCheck.IsAllowed)
                {
                    result.IsAllowed = false;
                    result.Reasons.AddRange(signatureCheck.Reasons);
                    result.SecurityLevel = SecurityLevel.InvalidSignature;
                }
            }

            // Content validation
            if (result.IsAllowed && _options.EnableContentValidation)
            {
                var contentCheck = await ValidateRequestContentAsync(context);
                if (!contentCheck.IsAllowed)
                {
                    result.IsAllowed = false;
                    result.Reasons.AddRange(contentCheck.Reasons);
                    result.SecurityLevel = SecurityLevel.InvalidContent;
                }
            }

            // Anomaly detection
            if (result.IsAllowed && _options.EnableAnomalyDetection)
            {
                var anomalyCheck = await DetectAnomaliesAsync(context);
                if (anomalyCheck.AnomalyScore > _options.AnomalyThreshold)
                {
                    result.AnomalyScore = anomalyCheck.AnomalyScore;
                    result.SecurityLevel = SecurityLevel.Suspicious;

                    if (anomalyCheck.AnomalyScore > _options.BlockingAnomalyThreshold)
                    {
                        result.IsAllowed = false;
                        result.Reasons.Add("High anomaly score detected");
                    }
                }
            }

            if (!result.IsAllowed)
            {
                _blockedRequestCount.Add(1,
                    new KeyValuePair<string, object?>("reason", result.SecurityLevel.ToString()),
                    new KeyValuePair<string, object?>("endpoint", context.Request.Path.ToString()));

                await LogSecurityEventAsync(context, result);
            }

            return result;
        }
        finally
        {
            _securityCheckDuration.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }

    private async Task<SecurityCheckResult> ValidateIpAddressAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };
        var clientIp = GetClientIpAddress(context);

        if (string.IsNullOrEmpty(clientIp))
        {
            result.IsAllowed = false;
            result.Reasons.Add("Unable to determine client IP address");
            return result;
        }

        // Check against blacklist
        if (_options.IpBlacklist.Contains(clientIp))
        {
            result.IsAllowed = false;
            result.Reasons.Add($"IP address {clientIp} is blacklisted");
            return result;
        }

        // Check against whitelist (if configured)
        if (_options.IpWhitelist.Any() && !_options.IpWhitelist.Contains(clientIp))
        {
            result.IsAllowed = false;
            result.Reasons.Add($"IP address {clientIp} is not whitelisted");
            return result;
        }

        // Geographic restrictions
        if (_options.EnableGeographicRestrictions)
        {
            var geoCheck = await ValidateGeographicLocationAsync(clientIp);
            if (!geoCheck.IsAllowed)
            {
                result.IsAllowed = false;
                result.Reasons.AddRange(geoCheck.Reasons);
            }
        }

        return result;
    }

    private async Task<SecurityCheckResult> CheckRateLimitAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };
        var clientIp = GetClientIpAddress(context);
        var endpoint = context.Request.Path.ToString();

        var key = $"{clientIp}:{endpoint}";
        var now = DateTime.UtcNow;

        var rateLimit = _rateLimitCache.AddOrUpdate(key,
            new RateLimitState { RequestCount = 1, WindowStart = now },
            (_, existing) =>
            {
                if (now - existing.WindowStart > _options.RateLimitWindow)
                {
                    return new RateLimitState { RequestCount = 1, WindowStart = now };
                }

                return existing with { RequestCount = existing.RequestCount + 1 };
            });

        if (rateLimit.RequestCount > _options.MaxRequestsPerWindow)
        {
            result.IsAllowed = false;
            result.Reasons.Add($"Rate limit exceeded: {rateLimit.RequestCount} requests in window");
        }

        return result;
    }

    private async Task<SecurityCheckResult> ValidateApiKeyAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };

        var apiKey = ExtractApiKey(context);
        if (string.IsNullOrEmpty(apiKey))
        {
            result.IsAllowed = false;
            result.Reasons.Add("API key is required but not provided");
            return result;
        }

        _apiKeyValidationCount.Add(1);

        var apiKeyInfo = await GetApiKeyInfoAsync(apiKey);
        if (apiKeyInfo == null)
        {
            result.IsAllowed = false;
            result.Reasons.Add("Invalid API key");
            return result;
        }

        if (apiKeyInfo.ExpiresAt < DateTime.UtcNow)
        {
            result.IsAllowed = false;
            result.Reasons.Add("API key has expired");
            return result;
        }

        if (!apiKeyInfo.IsActive)
        {
            result.IsAllowed = false;
            result.Reasons.Add("API key is disabled");
            return result;
        }

        // Check endpoint permissions
        var endpoint = context.Request.Path.ToString();
        if (apiKeyInfo.AllowedEndpoints.Any() &&
            !apiKeyInfo.AllowedEndpoints.Any(e => endpoint.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            result.IsAllowed = false;
            result.Reasons.Add("API key does not have permission for this endpoint");
            return result;
        }

        // Check method permissions
        var method = context.Request.Method;
        if (apiKeyInfo.AllowedMethods.Any() && !apiKeyInfo.AllowedMethods.Contains(method))
        {
            result.IsAllowed = false;
            result.Reasons.Add("API key does not have permission for this HTTP method");
            return result;
        }

        result.ApiKeyInfo = apiKeyInfo;
        return result;
    }

    private async Task<SecurityCheckResult> ValidateRequestSignatureAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };

        var signature = context.Request.Headers["X-Signature"].FirstOrDefault();
        var timestamp = context.Request.Headers["X-Timestamp"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            result.IsAllowed = false;
            result.Reasons.Add("Request signature and timestamp are required");
            return result;
        }

        if (!DateTimeOffset.TryParse(timestamp, out var requestTime))
        {
            result.IsAllowed = false;
            result.Reasons.Add("Invalid timestamp format");
            return result;
        }

        // Check timestamp freshness
        var age = DateTimeOffset.UtcNow - requestTime;
        if (age > _options.MaxRequestAge)
        {
            result.IsAllowed = false;
            result.Reasons.Add("Request timestamp is too old");
            return result;
        }

        // Validate signature
        var requestBody = await ReadRequestBodyAsync(context);
        var expectedSignature = GenerateRequestSignature(context, requestBody, timestamp);

        if (!signature.Equals(expectedSignature, StringComparison.Ordinal))
        {
            result.IsAllowed = false;
            result.Reasons.Add("Invalid request signature");
            return result;
        }

        return result;
    }

    private async Task<SecurityCheckResult> ValidateRequestContentAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };

        // Content-Type validation
        var contentType = context.Request.ContentType;
        if (!string.IsNullOrEmpty(contentType) &&
            _options.AllowedContentTypes.Any() &&
            !_options.AllowedContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase)))
        {
            result.IsAllowed = false;
            result.Reasons.Add($"Content-Type '{contentType}' is not allowed");
            return result;
        }

        // Content-Length validation
        if (context.Request.ContentLength > _options.MaxContentLength)
        {
            result.IsAllowed = false;
            result.Reasons.Add("Request content exceeds maximum allowed size");
            return result;
        }

        // JSON schema validation for JSON content
        if (contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var jsonValidation = await ValidateJsonContentAsync(context);
            if (!jsonValidation.IsAllowed)
            {
                result.IsAllowed = false;
                result.Reasons.AddRange(jsonValidation.Reasons);
            }
        }

        return result;
    }

    private async Task<AnomalyDetectionResult> DetectAnomaliesAsync(HttpContext context)
    {
        var result = new AnomalyDetectionResult();
        var clientIp = GetClientIpAddress(context);

        // Request frequency anomaly
        var requestPattern = AnalyzeRequestPattern(clientIp);
        if (requestPattern.AnomalyScore > 0)
        {
            result.AnomalyScore = Math.Max(result.AnomalyScore, requestPattern.AnomalyScore);
            result.Anomalies.Add("Unusual request frequency pattern");
        }

        // User agent anomaly
        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (IsAnomalousUserAgent(userAgent))
        {
            result.AnomalyScore = Math.Max(result.AnomalyScore, 0.7);
            result.Anomalies.Add("Suspicious user agent detected");
        }

        // Request size anomaly
        if (context.Request.ContentLength > 0)
        {
            var sizeAnomaly = AnalyzeRequestSize(context.Request.Path, context.Request.ContentLength.Value);
            if (sizeAnomaly > 0)
            {
                result.AnomalyScore = Math.Max(result.AnomalyScore, sizeAnomaly);
                result.Anomalies.Add("Unusual request size");
            }
        }

        return result;
    }

    private RequestPatternAnalysis AnalyzeRequestPattern(string clientIp)
    {
        var analysis = new RequestPatternAnalysis();
        var recentEvents = _securityEvents.Values
            .Where(e => e.ClientIp == clientIp && e.Timestamp > DateTime.UtcNow.AddMinutes(-10))
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (recentEvents.Count < 2) return analysis;

        // Calculate request intervals
        var intervals = new List<double>();
        for (int i = 1; i < recentEvents.Count; i++)
        {
            var interval = (recentEvents[i].Timestamp - recentEvents[i - 1].Timestamp).TotalSeconds;
            intervals.Add(interval);
        }

        // Detect patterns
        if (intervals.All(i => Math.Abs(i - intervals[0]) < 0.1)) // Too regular
        {
            analysis.AnomalyScore = 0.8;
            analysis.Pattern = "Automated/robotic request pattern";
        }
        else if (intervals.Count > 5 && intervals.Average() < 1.0) // Too fast
        {
            analysis.AnomalyScore = 0.9;
            analysis.Pattern = "Excessive request frequency";
        }

        return analysis;
    }

    private bool IsAnomalousUserAgent(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return true;

        var suspiciousPatterns = new[]
        {
            "bot", "crawler", "spider", "scraper", "python", "curl", "wget", "scanner"
        };

        return suspiciousPatterns.Any(pattern =>
            userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private double AnalyzeRequestSize(string endpoint, long contentLength)
    {
        // Simple heuristic: flag unusually large payloads for specific endpoints
        var typicalSizes = new Dictionary<string, long>
        {
            ["/api/auth"] = 1024,
            ["/api/payments"] = 4096,
            ["/api/users"] = 2048
        };

        foreach (var (pattern, expectedSize) in typicalSizes)
        {
            if (endpoint.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                if (contentLength > expectedSize * 10) // 10x larger than expected
                {
                    return Math.Min(0.9, contentLength / (double)(expectedSize * 10));
                }
                break;
            }
        }

        return 0.0;
    }

    private async Task<SecurityCheckResult> ValidateGeographicLocationAsync(string ipAddress)
    {
        var result = new SecurityCheckResult { IsAllowed = true };

        if (_options.BlockedCountries.Any() || _options.AllowedCountries.Any())
        {
            // In a real implementation, you would use a GeoIP service here
            // For now, we'll just return allowed
            _logger.LogDebug("Geographic validation requested for IP {IP}, but no GeoIP service configured", ipAddress);
        }

        return result;
    }

    private async Task<SecurityCheckResult> ValidateJsonContentAsync(HttpContext context)
    {
        var result = new SecurityCheckResult { IsAllowed = true };

        try
        {
            var body = await ReadRequestBodyAsync(context);
            if (!string.IsNullOrEmpty(body))
            {
                var jsonDoc = JsonDocument.Parse(body);

                // Basic JSON validation - in practice, you'd use JSON Schema
                if (ValidateJsonStructure(jsonDoc))
                {
                    result.IsAllowed = true;
                }
                else
                {
                    result.IsAllowed = false;
                    result.Reasons.Add("Invalid JSON structure");
                }
            }
        }
        catch (JsonException)
        {
            result.IsAllowed = false;
            result.Reasons.Add("Invalid JSON format");
        }

        return result;
    }

    private bool ValidateJsonStructure(JsonDocument jsonDoc)
    {
        // Implement your JSON validation logic here
        // This is a placeholder implementation
        return jsonDoc.RootElement.ValueKind == JsonValueKind.Object;
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    private string? ExtractApiKey(HttpContext context)
    {
        // Try header first
        var headerKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerKey))
            return headerKey;

        // Try authorization bearer
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader.Substring(7);

        // Try query parameter
        var queryKey = context.Request.Query["api_key"].FirstOrDefault();
        return queryKey;
    }

    private async Task<ApiKeyInfo?> GetApiKeyInfoAsync(string apiKey)
    {
        if (_apiKeyCache.TryGetValue(apiKey, out var cached))
        {
            return cached;
        }

        // In a real implementation, you would query a database or external service
        // For now, we'll create a mock API key
        var apiKeyInfo = new ApiKeyInfo
        {
            Key = apiKey,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            AllowedEndpoints = new List<string>(),
            AllowedMethods = new List<string> { "GET", "POST", "PUT", "DELETE" }
        };

        _apiKeyCache[apiKey] = apiKeyInfo;
        return apiKeyInfo;
    }

    private string GenerateRequestSignature(HttpContext context, string body, string timestamp)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var stringToSign = $"{method}\n{path}\n{body}\n{timestamp}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SignatureSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    private async Task<string> ReadRequestBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        context.Request.Body.Position = 0;
        return body;
    }

    private async Task LogSecurityEventAsync(HttpContext context, SecurityCheckResult result)
    {
        var securityEvent = new ApiSecurityEvent
        {
            Timestamp = DateTime.UtcNow,
            ClientIp = GetClientIpAddress(context),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Endpoint = context.Request.Path.ToString(),
            Method = context.Request.Method,
            SecurityLevel = result.SecurityLevel,
            Reasons = result.Reasons,
            AnomalyScore = result.AnomalyScore
        };

        var eventKey = $"{securityEvent.ClientIp}:{securityEvent.Timestamp.Ticks}";
        _securityEvents[eventKey] = securityEvent;

        _logger.LogWarning("Security check failed for {IP} accessing {Endpoint}: {Reasons}",
            securityEvent.ClientIp, securityEvent.Endpoint, string.Join(", ", securityEvent.Reasons));
    }

    private void PerformMaintenance(object? state)
    {
        // Cleanup old rate limit entries
        var rateLimitCutoff = DateTime.UtcNow.Subtract(_options.RateLimitWindow);
        var expiredRateLimit = _rateLimitCache
            .Where(kvp => kvp.Value.WindowStart < rateLimitCutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredRateLimit)
        {
            _rateLimitCache.TryRemove(key, out _);
        }

        // Cleanup old security events
        var eventCutoff = DateTime.UtcNow.AddHours(-24);
        var expiredEvents = _securityEvents
            .Where(kvp => kvp.Value.Timestamp < eventCutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredEvents)
        {
            _securityEvents.TryRemove(key, out _);
        }

        if (expiredRateLimit.Count > 0 || expiredEvents.Count > 0)
        {
            _logger.LogDebug("Cleaned up {RateLimit} rate limit entries and {Events} security events",
                expiredRateLimit.Count, expiredEvents.Count);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("API Security Hardening Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("API Security Hardening Service stopping");
        _maintenanceTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _maintenanceTimer?.Dispose();
    }
}

// Supporting classes and enums continue in next part...
public class ApiSecurityHardeningOptions
{
    public bool EnableRateLimiting { get; set; } = true;
    public int MaxRequestsPerWindow { get; set; } = 100;
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);

    public bool RequireApiKey { get; set; } = false;
    public bool RequireRequestSignature { get; set; } = false;
    public string SignatureSecret { get; set; } = "";
    public TimeSpan MaxRequestAge { get; set; } = TimeSpan.FromMinutes(5);

    public bool EnableContentValidation { get; set; } = true;
    public List<string> AllowedContentTypes { get; set; } = new();
    public long MaxContentLength { get; set; } = 1024 * 1024; // 1MB

    public List<string> IpWhitelist { get; set; } = new();
    public List<string> IpBlacklist { get; set; } = new();

    public bool EnableGeographicRestrictions { get; set; } = false;
    public List<string> AllowedCountries { get; set; } = new();
    public List<string> BlockedCountries { get; set; } = new();

    public bool EnableAnomalyDetection { get; set; } = true;
    public double AnomalyThreshold { get; set; } = 0.7;
    public double BlockingAnomalyThreshold { get; set; } = 0.9;
}

public class SecurityCheckResult
{
    public bool IsAllowed { get; set; } = true;
    public List<string> Reasons { get; set; } = new();
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Normal;
    public double AnomalyScore { get; set; } = 0.0;
    public ApiKeyInfo? ApiKeyInfo { get; set; }
}

public class ApiKeyInfo
{
    public string Key { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime ExpiresAt { get; set; }
    public List<string> AllowedEndpoints { get; set; } = new();
    public List<string> AllowedMethods { get; set; } = new();
}

public class ApiSecurityEvent
{
    public DateTime Timestamp { get; set; }
    public string ClientIp { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Method { get; set; } = "";
    public SecurityLevel SecurityLevel { get; set; }
    public List<string> Reasons { get; set; } = new();
    public double AnomalyScore { get; set; }
}

public record RateLimitState(int RequestCount, DateTime WindowStart);

public class AnomalyDetectionResult
{
    public double AnomalyScore { get; set; } = 0.0;
    public List<string> Anomalies { get; set; } = new();
}

public class RequestPatternAnalysis
{
    public double AnomalyScore { get; set; } = 0.0;
    public string Pattern { get; set; } = "";
}

public enum SecurityLevel
{
    Normal,
    Suspicious,
    Blocked,
    RateLimited,
    Unauthorized,
    InvalidSignature,
    InvalidContent
}