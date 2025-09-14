using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SmartPay.Api.Bootstrap;

namespace SmartPay.Api.Endpoints.Observability;

/// <summary>
/// Guarded scaling status endpoint. Returns minimal, redacted operational info
/// without PII or tenant-specific data. AdminOnly + feature flags required.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("ScalingAdmin")]
public sealed class ScalingStatusEndpoint(IOptions<FeaturesOptions> flags) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/scaling/status");
        AllowAnonymous(false);
        Policies("AdminOnly");
        // Note: rate limiter policy name via attribute
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var f = flags.Value;
        if (!(f.Scaling?.Enabled ?? false) || !(f.Scaling?.ExposeStatusEndpoint ?? false))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Minimal status payload (application/json) with no PII/tenant data
        var status = new
        {
            service = "smartpay",
            version = "wp7",
            timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            status = "healthy",
            // Redacted operational metrics - no tenant counts or PII
            metrics = new
            {
                uptime_seconds = (int)(DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime).TotalSeconds,
                memory_mb = GC.GetTotalMemory(false) / 1024 / 1024,
                threads = ThreadPool.ThreadCount
            }
        };

        await SendOkAsync(status, ct);
    }
}