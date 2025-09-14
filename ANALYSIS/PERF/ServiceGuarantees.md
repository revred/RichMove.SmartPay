# Service Guarantees & Performance Gates (Core API)

**Objective:** Detect regressions early, enforce absolute performance budgets, and track *best-known* results so we can tell whether a slowdown is due to code or load.

> **Release mode only:** All measurements and CI runs are performed against **Release** builds.

## SLO / SLI targets (Null providers, single-instance)

| Endpoint            | SLI                          | Target      | Window |
|---------------------|------------------------------|-------------|--------|
| `POST /fx/quote`    | p95 latency (ms)             | ≤ 150 ms    | CI run |
|                     | p99 latency (ms)             | ≤ 200 ms    | CI run |
|                     | Error rate (%)               | < 0.1 %     | CI run |

> These are **dev/CI baselines** with Null providers. Real provider latencies will be tracked separately once integrated.

## Gates
1. **Absolute**: if p95 or p99 exceeds targets, the perf job fails.
2. **Regression budget**: if a `BestKnown.yaml` exists, p95/p99 must not degrade by more than **15%** compared to best-known. (Set per-endpoint in `PerfGate.yaml`.)

## Workflow (CI)
1. CI builds and runs the API with `-c Release` on `http://localhost:5000`.
2. Perf runner generates traffic at a fixed RPS and duration, captures latencies, computes p50/p90/p95/p99, error rate.
3. Results are compared against `PerfGate.yaml` (absolute) and `BestKnown.yaml` (regression).
4. Artifacts (JSON + markdown report) are uploaded for review.

## Files
- `ANALYSIS/PERF/PerfGate.yaml` — absolute thresholds and regression budgets (source of truth).
- `ANALYSIS/PERF/Baselines/BestKnown.sample.yaml` — template; copy to `BestKnown.yaml` after an accepted green run to start regression tracking.

## Interpreting failures
- **Absolute fail** → likely code regression or environment slowdown. Inspect logs & CPU/memory in the runner output.
- **Regression fail** but absolute ok → likely small performance drift; investigate recent changes or increased allocations.