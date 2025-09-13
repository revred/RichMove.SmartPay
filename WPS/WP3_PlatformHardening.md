# WP3 — Platform Hardening (Post WP1+WP2)

## Goals
1. Lock the contract & error model
2. Finish feature‑flag plumbing (esp. blockchain OFF-by-default)
3. Land schema‑based contract tests
4. Stabilize CI gates

## Deliverables
- Feature flags bound from config; validated on startup
- ProblemDetails middleware default for all errors
- JSON Schemas + passing schema tests for public payloads
- Minimal provider stubs; Null & InMemory defaults
- CI: api‑contract lint, secret scan, format check

## Tasks
- [ ] Add `FeatureFlags` POCO + DI registration
- [ ] Add `UnhandledExceptionMiddleware` + `UseProblemDetails()` extension
- [ ] Publish OpenAPI + schemas; script `make contracts`
- [ ] Add schema tests in `Core.Tests` (no extra deps)
- [ ] Add `IClock`, `IIdGenerator` in Core
- [ ] Introduce `IBlockchainLedger` + Null/InMemory impls
- [ ] Document ADRs for flags, idempotency, ledger
- [ ] CI: add spectral lint & release drafter
- [ ] Write runbooks for toggling features safely

## Acceptance (DoD)
- All tests pass; new tests cover contracts
- API returns RFC7807 on all unhandled errors
- Ledger writes only when flag enabled