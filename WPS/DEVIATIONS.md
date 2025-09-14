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