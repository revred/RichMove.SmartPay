# Deviations & Clarifications (Steering Log)

> Maintained by Strategist/Steering. Flag anything that drifts from plan or needs a decision.

## 2025-09-14

### D1 — SignalR mapping style
- **Observation:** Early WP4 draft used `UseEndpoints(MapHub)`; WP4.1 refactored to `app.MapHub(...)` (minimal hosting alignment).
- **Action:** WP4 notes updated. **Canonical:** `MapHub`. No further action.

### D2 — Tenant strategy default
- **Observation:** Default strategy documented as `Host`; header override via `X-Tenant`. Code and docs aligned.
- **Action:** WP4 confirms default; WP4.1 tests cover both strategies.

### D3 — Event topic naming
- **Observation:** Topic chosen: `fx.quote.created`. Some docs referenced `updated` as future topic.
- **Action:** Standardize on `fx.quote.created` for WP4.1; leave `*.updated` as reserved. Docs updated.

### D4 — Metrics naming
- **Observation:** Meter name `SmartPay.Api`, counters `http.requests.total`, histogram `http.request.duration.ms`.
- **Action:** WPS references added; export naming to be revisited in observability hardening.

### D5 — Webhook signature header
- **Observation:** Header name `Richmove-Signature` fixed across docs and tests.
- **Action:** Keep consistent; add verifier samples in WP6 SDK work.

### D6 — RLS delete policy
- **Observation:** Delete intentionally restricted (service-role only).
- **Action:** Documented in RLS guide; acceptable for now.

## 2025-09-15 (Pragmatic tolerance)

### D7 — MVP allowlist for infra
- **Observation:** Some infra is already implemented and can be useful with tiny cost.
- **Decision:** Permit `/metrics` and `/scaling/status` in MVP **only with guardrails** (private binding + admin auth + no PII). Everything else remains parked.

### D8 — Cost levers
- **Single instance** App Service/ACA, **no persistent Prometheus** in RED, **short scrape interval**, **exporters off** for OTel. Output compression on.

### D9 — Guardrail testing requirement
- **Observation:** MVP-optional features need rigorous validation to prevent scope creep.
- **Decision:** All WP8 features require 404/401/200 test pattern + 7-day production validation before Core promotion.