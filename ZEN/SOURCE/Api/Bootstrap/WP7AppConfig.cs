using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

namespace SmartPay.Api.Bootstrap;

public static class WP7AppConfig
{
    public static IServiceCollection AddWp7Guardrails(this IServiceCollection services, IConfiguration cfg, ILoggerFactory? lf = null)
    {
        // Bind feature flags
        services.Configure<FeaturesOptions>(cfg.GetSection("Features"));

        // AdminOnly authorization policy
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAssertion(ctx =>
                {
                    var user = ctx.User;
                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        // Accept "Admin" role from common claim types
                        if (user.IsInRole("Admin")) return true;
                        foreach (var c in user.Claims)
                        {
                            if ((c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles"
                                 || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                                && c.Value == "Admin")
                                return true;
                        }
                    }

                    // Fallback Admin API key for RED/local
                    if (ctx.Resource is HttpContext httpCtx &&
                        httpCtx.Request.Headers.TryGetValue("X-Admin-Token", out var token))
                    {
                        var expected = cfg["Admin:ApiKey"];
                        if (TimingSafeEquals(token.ToString(), expected))
                            return true;
                    }
                    return false;
                });
            });
        });

        // Rate limiting: conservative per-user (admin) limits
        services.AddRateLimiter(_ => _
            .AddPolicy("MetricsAdmin", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User?.Identity?.Name ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10, // 10 rps
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }))
            .AddPolicy("ScalingAdmin", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User?.Identity?.Name ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, // 5 rps
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }))
        );

        return services;
    }

    public static IApplicationBuilder UseWp7Guardrails(this IApplicationBuilder app, IConfiguration cfg)
    {
        // Rate limiting & auth must be on the pipeline before endpoints
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }

    // Constant-time compare
    private static bool TimingSafeEquals(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        int diff = 0;
        for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
        return diff == 0;
    }
}

public sealed class FeaturesOptions
{
    public MonitoringOptions Monitoring { get; init; } = new();
    public ScalingOptions Scaling { get; init; } = new();

    public sealed class MonitoringOptions
    {
        public bool Enabled { get; init; }
        public bool Prometheus { get; init; }
        public bool OpenTelemetry { get; init; }
    }

    public sealed class ScalingOptions
    {
        public bool Enabled { get; init; }
        public bool ExposeStatusEndpoint { get; init; }
    }
}