# PERF Plan (WP4 bootstrap)

This folder scaffolds performance analysis work (moved under `ANALYSIS/` as agreed).

## Goals
1. Establish baseline API latency for `/health/*` and `/api/fx/quote` in **Release**.
2. Create YAML-driven perf scenarios (users, RPS, payload ranges).
3. Track p95/p99 latency and error budget per endpoint.

## Suggested Tooling
- `dotnet-counters` for ad‑hoc metrics
- `bombardier` or `wrk` for quick load probes
- `k6` for scripted scenarios (YAML → JSON transform step)

## Next (WP4.1)
- Add `ANALYSIS/PERF/scenarios/quote.yaml` and a tiny runner.
- Store results under `ANALYSIS/PERF/results/YYYYMMDD_hhmm/`.

## YAML Sketch
```yaml
target: "https://localhost:5001"
warmup_seconds: 10
duration_seconds: 60
endpoints:
  - name: fx-quote
    method: POST
    path: /api/fx/quote
    body_template:
      fromCurrency: USD
      toCurrency: GBP
      amount: [50, 100, 1000, 5000]
    rps: 25
```