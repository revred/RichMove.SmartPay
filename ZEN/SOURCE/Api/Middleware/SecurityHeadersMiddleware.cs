using Microsoft.Extensions.Options;
using System.Net;

namespace RichMove.SmartPay.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IOptions<SecurityHeadersOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ApplySecurityHeaders(context);

        if (IsPreflightRequest(context))
        {
            await HandlePreflightRequest(context);
            return;
        }

        await _next(context);

        ApplyResponseSecurityHeaders(context);
    }

    private void ApplySecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        if (_options.EnableHsts && context.Request.IsHttps)
        {
            headers.Append("Strict-Transport-Security",
                $"max-age={_options.HstsMaxAge}; includeSubDomains{(_options.HstsPreload ? "; preload" : "")}");
        }

        if (_options.EnableXFrameOptions)
        {
            headers.Append("X-Frame-Options", _options.XFrameOptionsValue);
        }

        if (_options.EnableXContentTypeOptions)
        {
            headers.Append("X-Content-Type-Options", "nosniff");
        }

        if (_options.EnableReferrerPolicy)
        {
            headers.Append("Referrer-Policy", _options.ReferrerPolicyValue);
        }

        if (_options.EnablePermissionsPolicy && !string.IsNullOrEmpty(_options.PermissionsPolicyValue))
        {
            headers.Append("Permissions-Policy", _options.PermissionsPolicyValue);
        }

        if (_options.EnableCrossDomainPolicy)
        {
            headers.Append("X-Permitted-Cross-Domain-Policies", "none");
        }

        if (_options.EnableXssProtection)
        {
            headers.Append("X-XSS-Protection", "1; mode=block");
        }

        if (_options.EnableExpectCt)
        {
            headers.Append("Expect-CT", $"max-age={_options.ExpectCtMaxAge}, enforce");
        }

        if (_options.EnableCacheControl)
        {
            var cacheDirectives = new List<string>();

            if (_options.NoCache) cacheDirectives.Add("no-cache");
            if (_options.NoStore) cacheDirectives.Add("no-store");
            if (_options.MustRevalidate) cacheDirectives.Add("must-revalidate");
            if (_options.MaxAge > 0) cacheDirectives.Add($"max-age={_options.MaxAge}");

            if (cacheDirectives.Any())
            {
                headers.Append("Cache-Control", string.Join(", ", cacheDirectives));
            }
        }

        if (_options.EnableServerHeader && !string.IsNullOrEmpty(_options.ServerHeaderValue))
        {
            headers.Append("Server", _options.ServerHeaderValue);
        }
        else if (!_options.EnableServerHeader)
        {
            headers.Remove("Server");
        }

        ApplyCorsHeaders(context);
    }

    private void ApplyCorsHeaders(HttpContext context)
    {
        if (!_options.EnableCors) return;

        var origin = context.Request.Headers.Origin.FirstOrDefault();
        var headers = context.Response.Headers;

        if (!string.IsNullOrEmpty(origin) && IsAllowedOrigin(origin))
        {
            headers.Append("Access-Control-Allow-Origin", origin);
            headers.Append("Vary", "Origin");
        }
        else if (_options.CorsAllowAnyOrigin)
        {
            headers.Append("Access-Control-Allow-Origin", "*");
        }

        if (_options.CorsAllowCredentials && !_options.CorsAllowAnyOrigin)
        {
            headers.Append("Access-Control-Allow-Credentials", "true");
        }

        if (_options.CorsAllowedMethods.Any())
        {
            headers.Append("Access-Control-Allow-Methods", string.Join(", ", _options.CorsAllowedMethods));
        }

        if (_options.CorsAllowedHeaders.Any())
        {
            headers.Append("Access-Control-Allow-Headers", string.Join(", ", _options.CorsAllowedHeaders));
        }

        if (_options.CorsExposedHeaders.Any())
        {
            headers.Append("Access-Control-Expose-Headers", string.Join(", ", _options.CorsExposedHeaders));
        }

        if (_options.CorsMaxAge > 0)
        {
            headers.Append("Access-Control-Max-Age", _options.CorsMaxAge.ToString());
        }
    }

    private void ApplyResponseSecurityHeaders(HttpContext context)
    {
        if (context.Response.StatusCode >= 400 && _options.RemoveServerHeaderOnError)
        {
            context.Response.Headers.Remove("Server");
        }

        if (_options.EnableSecurityHeadersLogging)
        {
            var appliedHeaders = context.Response.Headers
                .Where(h => IsSecurityHeader(h.Key))
                .Select(h => $"{h.Key}: {h.Value}")
                .ToList();

            if (appliedHeaders.Any())
            {
                _logger.LogDebug("Applied security headers for {Path}: {Headers}",
                    context.Request.Path, string.Join(", ", appliedHeaders));
            }
        }
    }

    private bool IsPreflightRequest(HttpContext context)
    {
        return context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) &&
               context.Request.Headers.ContainsKey("Access-Control-Request-Method");
    }

    private async Task HandlePreflightRequest(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync("");
    }

    private bool IsAllowedOrigin(string origin)
    {
        if (_options.CorsAllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            return true;

        foreach (var pattern in _options.CorsAllowedOriginPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(origin, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSecurityHeader(string headerName)
    {
        var securityHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Strict-Transport-Security",
            "X-Frame-Options",
            "X-Content-Type-Options",
            "Referrer-Policy",
            "Permissions-Policy",
            "X-Permitted-Cross-Domain-Policies",
            "X-XSS-Protection",
            "Expect-CT",
            "Content-Security-Policy",
            "Content-Security-Policy-Report-Only",
            "Access-Control-Allow-Origin",
            "Access-Control-Allow-Methods",
            "Access-Control-Allow-Headers",
            "Access-Control-Allow-Credentials",
            "Access-Control-Expose-Headers",
            "Access-Control-Max-Age"
        };

        return securityHeaders.Contains(headerName);
    }
}

public class SecurityHeadersOptions
{
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; } = false;

    public bool EnableXFrameOptions { get; set; } = true;
    public string XFrameOptionsValue { get; set; } = "DENY";

    public bool EnableXContentTypeOptions { get; set; } = true;

    public bool EnableReferrerPolicy { get; set; } = true;
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    public bool EnablePermissionsPolicy { get; set; } = true;
    public string PermissionsPolicyValue { get; set; } = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    public bool EnableCrossDomainPolicy { get; set; } = true;

    public bool EnableXssProtection { get; set; } = true;

    public bool EnableExpectCt { get; set; } = true;
    public int ExpectCtMaxAge { get; set; } = 86400; // 24 hours

    public bool EnableCacheControl { get; set; } = true;
    public bool NoCache { get; set; } = false;
    public bool NoStore { get; set; } = false;
    public bool MustRevalidate { get; set; } = true;
    public int MaxAge { get; set; } = 0;

    public bool EnableServerHeader { get; set; } = false;
    public string ServerHeaderValue { get; set; } = "";
    public bool RemoveServerHeaderOnError { get; set; } = true;

    public bool EnableSecurityHeadersLogging { get; set; } = false;

    // CORS Configuration
    public bool EnableCors { get; set; } = true;
    public bool CorsAllowAnyOrigin { get; set; } = false;
    public bool CorsAllowCredentials { get; set; } = false;
    public List<string> CorsAllowedOrigins { get; set; } = new();
    public List<string> CorsAllowedOriginPatterns { get; set; } = new();
    public List<string> CorsAllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    public List<string> CorsAllowedHeaders { get; set; } = new() { "Content-Type", "Authorization", "Accept", "X-Requested-With" };
    public List<string> CorsExposedHeaders { get; set; } = new();
    public int CorsMaxAge { get; set; } = 86400; // 24 hours

    // Content Security Policy
    public bool EnableContentSecurityPolicy { get; set; } = true;
    public string ContentSecurityPolicyValue { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";
    public bool ContentSecurityPolicyReportOnly { get; set; } = false;
    public string? ContentSecurityPolicyReportUri { get; set; }

    // Feature Policy / Permissions Policy
    public Dictionary<string, List<string>> FeaturePolicyDirectives { get; set; } = new()
    {
        ["accelerometer"] = new() { "'none'" },
        ["camera"] = new() { "'none'" },
        ["geolocation"] = new() { "'none'" },
        ["gyroscope"] = new() { "'none'" },
        ["magnetometer"] = new() { "'none'" },
        ["microphone"] = new() { "'none'" },
        ["payment"] = new() { "'none'" },
        ["usb"] = new() { "'none'" }
    };

    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}