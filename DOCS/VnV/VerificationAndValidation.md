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