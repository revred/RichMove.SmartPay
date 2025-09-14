# WP4.1 — Triggers & Perf Smoke

## Scope
- FX quote created → realtime trigger (middleware) and k6 smoke scenario.

## Deliverables
- `FxQuoteTriggerMiddleware`, `ANALYSIS/PERF/scenarios/fx-quote-smoke.js`, hub negotiate smoke test.

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E4.F5 | FX Quote Created Trigger | SMK-E4-Trigger-RoundTrip | Smoke_Features.md §3.3-C |
| E4.F6 | Perf smoke (k6) | PERF-E4-K6-Quote | `ANALYSIS/PERF/scenarios/fx-quote-smoke.js` |
| E4.F7 | Hub negotiation | SMK-E4-Hub-Negotiate | Smoke_Features.md §3.3-A |

### Acceptance
- Event received within 5s after quote; k6 p95 under guardrail.