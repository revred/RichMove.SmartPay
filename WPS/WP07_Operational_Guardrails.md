# WP07 — Operational Guardrails (Code Implementation)

> Purpose: Implement WP08 guardrails as working C# code. Transforms documentation into operational admin auth, feature flags, and guarded endpoints.

## Scope
- **AdminOnly authorization policy** with dual auth modes (role claims + API key fallback)
- **Feature flag configuration** (`FeaturesOptions`) with type-safe binding
- **Guarded endpoints** (`/metrics`, `/scaling/status`) with runtime feature validation
- **Rate limiting** (10 rps metrics, 5 rps scaling) via ASP.NET middleware
- **Unit tests** covering 404/401/200 guardrail patterns

## Deliverables (Code)
- `WP7AppConfig.cs`: Bootstrap configuration with AdminOnly policy and rate limiting
- `MetricsEndpoint.cs`: Prometheus-style metrics with feature flag guards
- `ScalingStatusEndpoint.cs`: Operational status with PII redaction
- `AdminPolicyTests.cs`: Comprehensive test coverage for all auth scenarios

## Implementation Details

### Admin Authentication
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAssertion(ctx =>
        {
            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                // Accept role claims
                if (user.IsInRole("Admin")) return true;
                foreach (var c in user.Claims)
                {
                    if ((c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
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
```

### Feature Flags
```csharp
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
```

### Rate Limiting
```csharp
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
```

## Security Features
- **Timing-safe string comparison** prevents side-channel attacks
- **PII redaction** in scaling endpoint (no tenant/user data)
- **Feature flag validation** ensures endpoints return 404 when disabled
- **Rate limiting** prevents admin endpoint abuse

## Testing Strategy
Comprehensive test coverage following the 404/401/200 pattern:
1. **404**: Feature flags disabled → endpoint not found
2. **401**: Feature enabled but no auth → unauthorized
3. **200**: Feature enabled + valid auth → success

### Test Cases
- `MetricsEndpoint_DisabledByDefault_Returns404`
- `ScalingStatusEndpoint_DisabledByDefault_Returns404`
- `MetricsEndpoint_NoAuth_Returns401`
- `ScalingStatusEndpoint_NoAuth_Returns401`
- `MetricsEndpoint_ValidAdminToken_Returns200`
- `ScalingStatusEndpoint_ValidAdminToken_Returns200`
- `MetricsEndpoint_InvalidAdminToken_Returns401`
- `ScalingStatusEndpoint_InvalidAdminToken_Returns401`

## Integration Requirements
To activate WP07 guardrails:

1. **Wire up configuration** in `Program.cs`:
   ```csharp
   builder.Services.AddWp7Guardrails(builder.Configuration);
   // ...
   app.UseWp7Guardrails(builder.Configuration);
   ```

2. **Environment configuration** (appsettings.json):
   ```json
   {
     "Features": {
       "Monitoring": {
         "Enabled": false,
         "Prometheus": false
       },
       "Scaling": {
         "Enabled": false,
         "ExposeStatusEndpoint": false
       }
     },
     "Admin": {
       "ApiKey": "secure-key-from-vault"
     }
   }
   ```

## Operational Notes
- **Default state**: All features disabled (404 responses)
- **Admin access**: Role claims preferred, API key fallback for RED/local
- **Cost impact**: Near-zero when disabled, minimal when enabled
- **Rollback**: Set feature flags to false → immediate 404 responses

## File Locations
- `ZEN/SOURCE/Api/Bootstrap/WP7AppConfig.cs` - Bootstrap configuration
- `ZEN/SOURCE/Api/Endpoints/Observability/MetricsEndpoint.cs` - Metrics endpoint
- `ZEN/SOURCE/Api/Endpoints/Observability/ScalingStatusEndpoint.cs` - Scaling endpoint
- `ZEN/TESTS/Api.Tests/WP7/AdminPolicyTests.cs` - Unit tests

---
**Status**: Implementation Complete
**Last Updated**: 2025-09-15
**Owner**: DevOps Team
**Next Step**: Integration with Program.cs and V&V documentation update