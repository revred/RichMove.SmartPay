# WP4 — Advanced Features (Scaffold → Feature)

## Scope
- SignalR notifications hub with tenant-scoped groups
- Hook points ready for domain events (WP4.2).

## 2) Multi-tenancy plumbing
- Ambient `TenantContext` resolved from `X-Tenant` header or subdomain.
- `ITenantResolver` default strategy: `Host` or `Header` (configurable).
- Middleware that sets `TenantContext.Current` per request.

## 3) Analytics (lightweight)
- Request logging middleware (structured logs).
- Endpoint and status code counters via `System.Diagnostics.Metrics`.
- Optional: surface `/metrics` endpoint in WP4.3.

---

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E4.F1 | Notifications Hub | SMK-E4-Hub-Negotiate | Smoke_Features.md §3.3-A |
| E4.F2 | Notification Service (In-Memory) | INTEG-E4-Notify-Loopback | Covered via trigger E4.F5 |
| E4.F3 | Multi-tenancy middleware | SMK-E4-Tenant-Header | Smoke_Features.md §3.3-B |
| E4.F3.N1 | Tenant resolver strategies | UNIT-E4-TenantResolver | `ZEN/TESTS/WP4/TenantResolverTests.cs` |
| E4.F4 | Analytics counters/logs | OBS-E4-Metrics | Logs/metrics (optional PR) |

### Acceptance
- Hub negotiate not 404; header strategy functional; resolver unit tests passing.

### Notes / Clarifications
- **Mapping:** `MapHub` is the canonical mapping (replacing `UseEndpoints`) from WP4.1 onward.