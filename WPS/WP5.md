# WP5 — Event Bridge & Tenant Isolation (Implementation)

**Goal:** Bridge domain events outward (webhooks) and finalize multi-tenant isolation at the DB layer (RLS), without adding runtime bloat or heavy deps.

## Scope
1. **Outbound Webhooks**
   - HMAC signature: `Richmove-Signature: t=<unix>, v1=<hex>` on request body.
   - Background **retrying outbox** (in-memory Channel) with exponential backoff (max 5 attempts).
   - **Composite Notification Service** that mirrors events to SignalR and webhooks.
   - Configurable per-tenant or global endpoints.
2. **Supabase RLS Templates**
   - SQL policies for `quotes` table demonstrating tenant isolation with JWT claims.
   - Guide to enable RLS and test with service-key vs anon-key clients.

## Out-of-scope (deferred)
- Provider-specific Realtime (Supabase Phoenix channels) — may land in WP5.2 if we add a thin dependency.
- Delivery DLQ persistence — current outbox is volatile; persisting queue can be added in WP5.1.

## Configuration
Add `appsettings.WP5.sample.json` and selectively merge:
```json
{
  "WP5": {
    "Webhooks": {
      "Enabled": true,
      "Endpoints": [
        { "Name": "AuditSink", "Url": "https://example.com/hooks/audit", "Secret": "replace-me", "Active": true }
      ],
      "TimeoutSeconds": 5,
      "MaxAttempts": 5,
      "InitialBackoffMs": 300
    }
  }
}
```

## Wire-up
```csharp
builder.Services.AddWp5Features(builder.Configuration);
app.UseWp5Features(builder.Configuration);
```

## Testing
- Unit tests cover signature calculation and composite dispatch fan-out.
- Smoke: set one test endpoint (httpbin or mock) and POST a quote to observe a webhook (see `Smoke_Features.md` §3.3).

## RLS (Supabase)
See `DB/SUPABASE/WP5_RLS.sql` and `DOCS/Data/RLS_Supabase.md` for enabling per-tenant isolation.

---

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E4.F2 (extended) | Composite notifications (SignalR + Webhooks) | INTEG-WP5-Composite | Manual smoke + logs |
| E5.F? | Webhook signature (HMAC) | UNIT-WP5-Signer | `ZEN/TESTS/Api.Tests/WebhookSignerTests.cs` |
| E5.F? | Outbox retry | OBS-WP5-Outbox | Logs show retries/backoff |
| E5.F? | Webhook delivery | SMK-WP5-Webhook-Delivery | Configured endpoint receives POST |
| E2.F2 (DB) | RLS tenant isolation | INTEG-WP5-RLS | DB script applied; anon vs service role behavior |

### Acceptance
- When enabled, `fx.quote.created` mirrors to at least one configured webhook with valid `Richmove-Signature`.
- RLS policies enforce tenant row visibility (as per guide).