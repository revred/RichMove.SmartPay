# Load Test Plan (k6)

- Smoke: 5 VUs, 30s (CI)
- Baseline: 50 VUs, 5m, ramping
- Goal: 95p < 200ms @ 100 RPS
- Scenarios: checkout create, webhook delivery storms, FX quotes.
