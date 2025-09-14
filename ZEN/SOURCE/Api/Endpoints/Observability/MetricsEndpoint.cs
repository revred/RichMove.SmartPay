using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SmartPay.Api.Bootstrap;

namespace SmartPay.Api.Endpoints.Observability;

/// <summary>
/// Guarded Prometheus-style metrics endpoint. Outputs a minimal, non-sensitive payload.
/// Binding to private/loopback is recommended at hosting layer; this endpoint also
/// requires AdminOnly and feature flags to be ON.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("MetricsAdmin")]
public sealed class MetricsEndpoint(IOptions<FeaturesOptions> flags) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/metrics");
        AllowAnonymous(false);
        Policies("AdminOnly");
        // Note: rate limiter policy name via attribute
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var f = flags.Value;
        if (!(f.Monitoring?.Enabled ?? false) || !(f.Monitoring?.Prometheus ?? false))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Minimal stub payload (text/plain) to keep cost near-zero until full exporter is wired.
        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.StartAsync(ct);
        await HttpContext.Response.WriteAsync("# TYPE smartpay_info gauge\n", ct);
        await HttpContext.Response.WriteAsync("smartpay_info{service=\"smartpay\",version=\"wp7\"} 1\n", ct);
    }
}