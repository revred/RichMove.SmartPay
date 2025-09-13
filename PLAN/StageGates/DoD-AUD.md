# DoD — WP3 Platform Hardening + AUD First-Class Support

This Definition of Done extends the universal DoD with **currency expansion** acceptance for **AUD** (treated at par with GBP).

## Build & Quality
- [ ] All projects build in Release with warnings-as-errors.
- [ ] Unit + integration + contract tests pass (incl. AUD corridors).
- [ ] Mutation baseline recorded; no regressions vs last run.

## Contracts & Compatibility
- [ ] OpenAPI v1.1 published (`DOCS/API/SmartPay.OpenAPI.v1.1.yaml`) with AUD examples.
- [ ] JSON Schemas v1.1 added for `FxQuoteRequest/Result` (generic ISO‑4217 codes).
- [ ] ProblemDetails catalog updated (`DOCS/ERRORS/ErrorCatalog.v1.md`).
- [ ] No breaking changes to v1.0 endpoints; examples enriched only.

## Security & Compliance
- [ ] Secrets policy verified; no keys in repo.
- [ ] Threat model updated to reflect currency expansion (no new trust boundaries).
- [ ] CodeQL workflow active and green.

## Operational Readiness
- [ ] Health endpoints return 200 with AUD examples available in Postman.
- [ ] Runbooks updated for toggling currency allowlists (if used).
- [ ] SLO doc updated (no change to targets).

## Documentation
- [ ] Currency roster updated with **AUD** Tier‑1.
- [ ] ADR recorded for currency configuration.
- [ ] Postman v1.1 collection added with AUD corridors.

## Out‑of‑Scope (future)
- Provider‑specific pricing/fees for AUD corridors (lives in provider layer).