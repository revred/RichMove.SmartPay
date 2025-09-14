# Admin Auth — `RequireRole("Admin")` Semantics

**Goal:** A clear, minimal mechanism to protect privileged endpoints like `/metrics` and `/scaling/status`.

## Policy
We define an **authorization policy** called `AdminOnly` which passes if **any** of the following are true:
1. The user principal has a role claim equal to `Admin` — we accept:
   - `roles` or `role` claim with value `Admin`, **or**
   - the .NET role URI claim (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) with value `Admin`.
2. A valid **Admin API key** is presented in header `X-Admin-Token` (fallback for RED/local).

> **Why fallback?** In RED/local we may not have full identity configured. The API key is an **interim** gate with rotation and audit; in GREEN we prefer OIDC/Entra roles or group-based access.

## Reference Implementation (C# snippet)
_Docs-only; to be added in a follow-up code patch if desired._

```csharp
// Program.cs (excerpt)
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User != null && (
                ctx.User.IsInRole("Admin") ||
                ctx.User.Claims.Any(c =>
                    (c.Type == "role" || c.Type == "roles" ||
                     c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    && c.Value == "Admin"
                ) ||
                // API key fallback for RED/local
                (ctx.Resource as HttpContext)?.Request.Headers.TryGetValue("X-Admin-Token", out var token) == true &&
                TimingSafeEquals(token.ToString(), cfg["Admin:ApiKey"])
            )
        ));
});

static bool TimingSafeEquals(string a, string b)
{
    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
    var ba = System.Text.Encoding.UTF8.GetBytes(a);
    var bb = System.Text.Encoding.UTF8.GetBytes(b);
    if (ba.Length != bb.Length) return false;
    int diff = 0;
    for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
    return diff == 0;
}
```

**Controller usage:**
```csharp
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("[controller]")]
public class MetricsController : ControllerBase { /* ... */ }
```

## Identity Sources
- **GREEN (prod):** OIDC (e.g., Entra ID) → map admin users or groups to `Admin` role claim.
- **RED (dev/free tier):** `X-Admin-Token` header using `Admin:ApiKey` from secure secret store (not in repo).

## Operational Notes
- **Rotation:** rotate `Admin:ApiKey` quarterly; store only in Key Vault/GH secrets.
- **Audit:** log admin access attempts (userId or `apiKey` hash prefix), but **never** the raw key.
- **Defense in depth:** private binding for `/metrics`, firewall/VNet rules where possible.

## Security Properties
- **Timing-safe comparison** prevents timing-based side-channel attacks
- **Multiple claim types** supported for different identity providers
- **Audit-friendly** logging with hash prefixes only
- **Environment-appropriate** auth methods (OIDC for prod, API key for dev)

## Testing
Admin auth must be validated with the guardrail test pattern:
- **404**: Feature disabled → endpoint returns 404
- **401**: Feature enabled but no/invalid auth → returns 401
- **200**: Feature enabled + valid admin auth → returns 200

## Implementation Checklist
- [ ] `AdminOnly` policy configured in DI container
- [ ] Controllers decorated with `[Authorize(Policy = "AdminOnly")]`
- [ ] Admin API key stored in secure configuration (not in repo)
- [ ] Audit logging implemented for admin access attempts
- [ ] Guardrail tests implemented (404/401/200 pattern)
- [ ] Rate limiting applied (10 rps per admin recommended)
- [ ] Private binding configured for sensitive endpoints in RED environment