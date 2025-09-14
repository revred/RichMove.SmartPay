# Feature Inventory (Living)

This is the human-friendly version of `SmartPay_Feature_Inventory.csv`. Update after each WP. See `Smoke_Features.md` for probes.

## Implemented
- **E1** Platform Foundation — API shell, Swagger, health, CI.
- **E2** FX Core — Create Quote, persistence, background pricing, DB health.
- **E4** Advanced — SignalR hub, notification service, tenancy middleware, analytics, FX-quote trigger.
- **WP5 (this patch)** — Outbound webhooks (signed + retry), RLS templates.

## Planned
- **E3** Payment Provider Orchestration.
- **E7/E8 (WP6)** Blazor SSR UI + SDKs + low-cost hosting.

> The CSV remains the canonical index for automated test generation.