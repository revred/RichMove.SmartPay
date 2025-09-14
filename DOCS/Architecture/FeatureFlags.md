# Feature Flags — Policy & Defaults

Purpose: keep MVP lean and costs low by gating optional infrastructure/features. Some guardrailed items may be enabled in MVP.

## Principles
- **Default OFF** unless the feature directly benefits MVP users.
- **Explicit enable** in GREEN (prod) after cost/value review.
- **Quick disable** if costs creep up or issues arise.
- **Clear ownership** for each flag and promotion decisions.

## Suggested flags (names illustrative)
```jsonc
{
  "Features": {
    "Monitoring": {
      "Enabled": false,        // RED: false, GREEN: true (selective)
      "Prometheus": false,     // RED: false, GREEN: true (private bind + auth)
      "OpenTelemetry": false   // RED: false, GREEN: true (exporters usually off)
    },
    "Scaling": {
      "Enabled": false,
      "ExposeStatusEndpoint": false
    },
    "Advanced": {
      "BlockchainStubs": false,
      "NonFxDomains": false
    }
  }
}
```

### Example bindings (ASP.NET)
- Metrics route bound to `http://127.0.0.1:XXXX/metrics` or private VNet address.
- Admin auth enforced via policy `RequireRole('Admin')` on both endpoints.

### Recommended rate limits/timeouts
- `/metrics`: **10 rps per admin**, 2s timeout.
- `/scaling/status`: **5 rps per admin**, 2s timeout.
- Apply ASP.NET Rate Limiting middleware when we wire flags in code.

## Environments
- **RED (dev/free tier):** all OFF, except minimal logs & health.
- **GREEN (prod/paid):** selectively enable with cost review.

## V&V
- Promotion of any flag to `true` requires: updated `TraceabilityMatrix.csv`, WP8 acceptance, and smoke coverage.

### Guardrail smoke (conceptual)
- SMK-E8-Metrics-404: flags off → `/metrics` returns 404.
- SMK-E8-Metrics-401: monitoring on but not authed → 401.
- SMK-E8-Metrics-200: monitoring on + admin → 200.
- SMK-E8-Scaling-404/401/200: analogous for `/scaling/status`.

## Configuration Examples

### RED Environment (Development)
```json
{
  "Features": {
    "Monitoring": {
      "Enabled": false,
      "Prometheus": false,
      "OpenTelemetry": false
    },
    "Scaling": {
      "Enabled": false,
      "ExposeStatusEndpoint": false
    }
  },
  "Admin": {
    "ApiKey": "*** stored in secure configuration only ***"
  }
}
```

### GREEN Environment (Production) - Example Selective Enable
```json
{
  "Features": {
    "Monitoring": {
      "Enabled": true,
      "Prometheus": true,
      "OpenTelemetry": true
    },
    "Scaling": {
      "Enabled": true,
      "ExposeStatusEndpoint": true
    }
  },
  "Monitoring": {
    "BindAddress": "127.0.0.1:8080",
    "ScrapeInterval": "60s",
    "MaxScrapeRps": 10
  }
}
```

## Implementation Guidelines

### Controller Gating Pattern
```csharp
[ApiController]
[Route("[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IConfiguration _config;

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult GetMetrics()
    {
        if (!_config.GetValue<bool>("Features:Monitoring:Enabled") ||
            !_config.GetValue<bool>("Features:Monitoring:Prometheus"))
        {
            return NotFound(); // 404 when disabled
        }

        // Return metrics...
    }
}
```

### Startup Registration Pattern
```csharp
// Program.cs
if (configuration.GetValue<bool>("Features:Monitoring:Enabled"))
{
    services.AddSingleton<IMetricsCollector, PrometheusMetricsCollector>();

    if (configuration.GetValue<bool>("Features:Monitoring:Prometheus"))
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers(); // Includes MetricsController
    }
}
```

## Security Requirements

### Admin Authentication
All guarded endpoints MUST use the `AdminOnly` policy:
- Supports role claims (`Admin` role) OR
- Supports `X-Admin-Token` header (RED environment fallback)
- See `DOCS/Security/AdminAuth.md` for implementation details

### Private Binding (RED Environment)
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Metrics": {
        "Url": "http://127.0.0.1:8080"
      }
    }
  }
}
```

### Rate Limiting
```csharp
services.AddRateLimiter(options =>
{
    options.AddPolicy("AdminEndpoints", httpContext =>
        RateLimitPartition.CreateFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

## Cost Control Integration

### Budget Triggers
When cost alerts are triggered:
1. **50% budget** → Review enabled features, document current usage
2. **80% budget** → Disable non-essential WP8 features
3. **100% budget** → Emergency disable all optional features

### Feature Cost Attribution
Track costs for each enabled feature:
- `Monitoring.Prometheus` → CPU overhead, egress for scraping
- `Scaling.ExposeStatusEndpoint` → Minimal (only if accessed)
- `OpenTelemetry` → Memory overhead, potential egress

## Operational Procedures

### Feature Enable Checklist
Before enabling any WP8 feature in production:
- [ ] Cost impact assessed and approved
- [ ] Admin authentication configured
- [ ] Rate limiting implemented
- [ ] Monitoring alerts configured
- [ ] Rollback procedure tested
- [ ] Guardrail smoke tests passing

### Feature Disable Procedure
To quickly disable a feature:
1. Set feature flag to `false` in configuration
2. Deploy configuration change (or restart if needed)
3. Verify endpoints return 404
4. Monitor for cost reduction
5. Document incident if emergency disable

### Monthly Feature Review
- Review cost attribution for each enabled feature
- Verify security posture (admin access logs)
- Check alert noise and threshold tuning
- Evaluate promotion candidates (MVP-optional → Core)

## Monitoring and Alerting

### Feature Flag Metrics
Track these metrics for operational visibility:
- Feature enable/disable events
- Admin endpoint access patterns
- Cost per enabled feature
- Alert frequency per feature

### Dashboard Queries
```kusto
// Feature access patterns
requests
| where url contains "/metrics" or url contains "/scaling"
| summarize Count = count() by bin(timestamp, 1h), url
| render timechart

// Admin authentication success/failure
SecurityEvent
| where EventID in (4624, 4625) // Success/failure logon
| where Account contains "admin" or Process contains "X-Admin-Token"
| summarize by bin(TimeGenerated, 1h), EventID
```

## Integration with V&V

All WP8 features must pass guardrail testing:
- **404 Test**: Feature disabled → endpoint returns 404
- **401 Test**: Feature enabled but no auth → returns 401
- **200 Test**: Feature enabled + valid admin auth → returns 200

These tests must be:
- Automated in CI/CD pipeline
- Included in TraceabilityMatrix.csv
- Verified before production deployment