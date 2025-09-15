# Verification & Validation (V&V) — Model & Process

**Goal:** Every feature has a purpose, a test, and evidence. We prevent bloat and regressions by maintaining strict traceability.

## IDs & Taxonomy
- **Feature IDs:** `E#`, `E#.F#`, `E#.F#.N#`, optional `R#` for requirements at any level.
- **Test IDs:** `SMK-*` (smoke), `UNIT-*`, `INTEG-*`, `PERF-*`, `OBS-*` (observability checks).

## Traceability
`FeatureID → TestIDs → WorkPackage → Evidence`

**Sources:**
- `WPS/*` — WP plans with V&V tables.
- `DOCS/VnV/TraceabilityMatrix.csv` — master, machine-readable index.
- `Smoke_Features.md` — runnable smoke playbook.

## Process
1. Propose/change feature → add rows to WP V&V table and TraceabilityMatrix.
2. Add/adjust smoke/other tests; ensure IDs match.
3. CI runs PR smoke subset; nightly full matrix.
4. Evidence stored in CI logs, artifacts, or linked dashboards.

## Acceptance Gates
- No WP merges without: updated V&V table, matrix rows, and at least one smoke probe.

## Evidence Examples
- Smoke outputs (HTTP status/JSON fields).
- Unit test results (xUnit).
- k6 thresholds.
- Logs/metrics screenshots or text.

---

## Guardrail Testing (WP8 Features)

For MVP-optional features with guardrails, we require the **404/401/200 test pattern**:

### Test Pattern Requirements
Every guarded endpoint must validate:
- **404 Test**: Feature disabled → endpoint returns 404
- **401 Test**: Feature enabled but no/invalid auth → returns 401
- **200 Test**: Feature enabled + valid admin auth → returns 200

### Implementation Guidelines
```bash
# Example guardrail smoke tests
SMK-E8-Metrics-404:
  curl -f http://localhost:5000/metrics
  expect: 404 (when Features.Monitoring.Enabled=false)

SMK-E8-Metrics-401:
  curl -f http://localhost:5000/metrics
  expect: 401 (when enabled but no X-Admin-Token)

SMK-E8-Metrics-200:
  curl -H "X-Admin-Token: $ADMIN_KEY" http://localhost:5000/metrics
  expect: 200 + prometheus metrics content
```

## Promotion Criteria (MVP‑optional → Core)
To **graduate** an MVP-optional feature to Core, we require a 7‑day clean run in production canary with:
1) Security: admin auth enforced; no unauth requests; no high vulns.
2) Performance: p95 latency delta ≤ +5% vs baseline; no stability regressions.
3) Cost: marginal monthly delta ≤ £15; budgets clean (no 80%/100% breach).
4) Reliability: error rates unchanged; health probes green; no crash loops.
5) V&V: guardrail smoke tests in CI (404/401/200) + updated Traceability Matrix.
6) Operations: documented runbook (enable/disable, rollback, dashboards).

Fail any? Revert to MVP-optional and fix before next attempt.

## CI Gates
- Guardrails workflow runs unit tests, checks V&V artifacts, and enforces **flag defaults are false** in samples.
- Extend with smoke runners as we add headless scenarios.