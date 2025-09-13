# Stage Gates & Definition of Done

## Stage Gates (Go/No-Go)
- **SG1 (End WP1)**: CI running; coverage gate active; mutation runner wired. **No-Go** if coverage gate not enforced.
- **SG2 (End WP4/5)**: Orchestrator routes payments in sandbox; FX quotes snapshot with orders; webhooks unified. **No-Go** if idempotency not proven.
- **SG3 (MVP Demo m1)**: Checkout flow with Stripe+TrueLayer works; regression suite green; k6 smoke passes.
- **SG4 (Beta m2)**: Disputes webhooks, payouts (PSP default) + SPI remittance path proven; analytics v1 live; security review passed.
- **SG5 (Reg readiness)**: SPI pack complete; partner agent onboarding greenlit; wind-down and outsourcing docs signed.

## Definition of Done (per WP)
- All tasks complete & merged via PR with **tests**.
- **Coverage ≥99%** and **mutation ≥95%** on affected projects.
- Contract tests (Schemathesis/Dredd) green for changed endpoints.
- Security static analysis: no criticals/highs.
- Docs updated (README, ADRs, API spec).
- Gantt updated; risks & decisions logged.
