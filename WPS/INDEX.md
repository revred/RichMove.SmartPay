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
| WP01 | Repository & Tooling | Feature/Hardening | E1 | API shell, CI/CD, health checks | `WPS/WP01_Repository_and_Tooling_V2.md#vv` |
| WP02 | Core Domain & Database | Feature | E2 | Domain models, RLS, audit | `WPS/WP02_Core_Domain_and_DB_V2.md#vv` |
| WP03 | API & Contracts | Feature | E3 | FastEndpoints, OpenAPI | `WPS/WP03_API_and_Contracts_V2.md#vv` |
| WP04 | Payment Orchestrator & Connectors | Feature | E3/E4 | SignalR, multi-tenancy | `WPS/WP04_Payment_Orchestrator_and_Connectors_V2.md#vv` |
| WP05 | Event Bridge & Tenant Isolation | Feature | E4/E5 | Webhooks, RLS templates | `WPS/WP05_Event_Bridge_and_Tenant_Isolation_V2.md#vv` |
| WP06 | Checkout UI & SDKs | Plan | E7 | Blazor SSR, SDK generation | `WPS/WP06_Checkout_UI_and_SDKs_V2.md#vv` |
| WP07 | Merchant Dashboard | Plan | E7 | Admin UI, CRUD operations | `WPS/WP07_Merchant_Dashboard_V2.md#vv` |
| WP08 | Analytics & Reporting | Plan | E4/E6 | Business intelligence, metrics | `WPS/WP08_Analytics_and_Reporting_V2.md#vv` |
| WP09 | Security & Compliance Controls | Scaffold | E5 | Security framework, monitoring | `WPS/WP09_Security_and_Compliance_Controls_V2.md#vv` |
| WP10 | Quality CI/CD & Testing | Scaffold | E6 | Advanced testing, dev tools | `WPS/WP10_Quality_CICD_and_Testing_V2.md#vv` |
| WP11 | Regulatory & Licensing | Plan | E9 | Compliance framework, licensing | `WPS/WP11_Regulatory_and_Licensing_V2.md#vv` |
| WP12 | Partner Integrations & GTM | Plan | E10 | Partner framework, hosting | `WPS/WP12_Partner_Integrations_and_GTM_V2.md#vv` |

> V&V hub: see `DOCS/VnV/VerificationAndValidation.md` and `DOCS/VnV/TraceabilityMatrix.csv`.

## Naming & IDs
- **Features:** `E#`, `E#.F#`, `E#.F#.N#`, requirements `R#` at any level.
- **Tests:** `SMK-*` (smoke), `UNIT-*`, `INTEG-*`, `PERF-*`.
- **Traceability Key:** `FeatureID -> TestIDs -> WorkPackage`.

## Change Control
- New features must add rows to the traceability matrix and a smoke probe.
- Docs and CSV stay in lock‑step with each WP merge.