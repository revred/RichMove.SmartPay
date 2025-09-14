# WP4 — Advanced Features (Scaffold)

Scope tracked here is intentionally **scaffold-first** to keep changes low-risk while enabling rapid iteration.

## 1) Real-time notifications
- Add SignalR hub at `/hubs/notifications` (per-tenant groups).
- Notification service abstraction with in-memory default.
- Hook points ready for domain events (WP4.2).

## 2) Multi-tenancy plumbing
- Ambient `TenantContext` resolved from `X-Tenant` header or subdomain.
- `ITenantResolver` default strategy: `Host` or `Header` (configurable).
- Middleware that sets `TenantContext.Current` per request.

## 3) Analytics (lightweight)
- Request logging middleware (structured logs).
- Endpoint and status code counters via `System.Diagnostics.Metrics`.
- Optional: surface `/metrics` endpoint in WP4.3.

## 4) Configuration
Add `appsettings.WP4.sample.json` then merge into your environment files.

```json
{
  "WP4": {
    "Notifications": { "Enabled": true, "Provider": "InMemory" },
    "MultiTenancy": { "Enabled": true, "Strategy": "Host", "Header": "X-Tenant" },
    "Analytics": { "Enabled": true, "RequestLogging": true }
  }
}
```

## 5) Wire-up in Program.cs
```csharp
using SmartPay.Api.Bootstrap;
builder.Services.AddWp4Features(builder.Configuration);
app.UseWp4Features(builder.Configuration);
```

## 6) Tests (WP4.2+)
- Hub smoke tests to ensure mapping and group join.
- Tenant resolution unit tests (host/header).
- Metrics counters presence & increment tests.

## 6.1) **WP4.1 Triggers**

### FX Quote → Realtime
- A middleware inspects successful responses from **POST** `/api/fx/quote`,
  parses JSON, and publishes `fx.quote.created` to the tenant group.
- No controller changes required. Config toggle:

```json
{ "WP4": { "Triggers": { "FxQuoteCreated": true } } }
```

### Tests
- **NotificationsHub smoke test** checks that `/hubs/notifications/negotiate` is reachable.
- **TenantResolver unit tests** validate Host/Header strategies.

### Perf
- `ANALYSIS/PERF/scenarios/fx-quote-smoke.js` — quick k6 smoke to validate baseline.

## 7) Out-of-scope for WP4 (defer to WP5)
- Supabase Realtime wiring for notifications (stub exists).
- Multi-tenant data isolation at DB level (schema/row filter).
- Analytics dashboard UI.