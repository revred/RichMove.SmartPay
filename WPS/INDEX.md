# Work Packages — Master Index (V&V aligned)

**Purpose:** Single entry point for scope, status and **Verification & Validation** (V&V) traceability.

## Status Legend
- **Plan** — documentation only
- **Scaffold** — wiring, placeholders
- **Feature** — end-to-end behavior behind flags
- **Hardening** — perf, cost, security, docs

## Table
| WP | Title | Status | Primary Epics | Key Artifacts | V&V Tables |
|---:|-------|--------|---------------|---------------|------------|
| WP1 | Platform Foundation | Feature/Hardening | E1 | `README.md`, API shell, health | `WPS/WP1.md#vv` |
| WP2 | Foreign Exchange Core | Feature | E2 | FX quote endpoint, storage | `WPS/WP2.md#vv` |
| WP3 | Payment Providers | Plan | E3 | Orchestration plan | `WPS/WP3.md#vv` |
| WP4 | Advanced (Realtime/Tenancy/Analytics) | Feature | E4 | Hub, tenancy, metrics | `WPS/WP4.md#vv` |
| WP4.1 | Triggers & Perf Smoke | Feature | E4 | Quote trigger, k6 | `WPS/WP4.1.md#vv` |
| WP5 | Event Bridge & RLS | Feature | E4/E5 | Webhooks, RLS templates | `WPS/WP5.md#vv` |
| WP6 | UI & SDK | Plan | E7/E8 | Blazor SSR plan, SDK plan | `WPS/WP6.md#vv` |

> V&V hub: see `DOCS/VnV/VerificationAndValidation.md` and `DOCS/VnV/TraceabilityMatrix.csv`.

## Naming & IDs
- **Features:** `E#`, `E#.F#`, `E#.F#.N#`, requirements `R#` at any level.
- **Tests:** `SMK-*` (smoke), `UNIT-*`, `INTEG-*`, `PERF-*`.
- **Traceability Key:** `FeatureID -> TestIDs -> WorkPackage`.

## Change Control
- New features must add rows to the traceability matrix and a smoke probe.
- Docs and CSV stay in lock‑step with each WP merge.