# WP8 — Advanced Infra & Deployment (MVP-optional, Guardrailed)

> Purpose: Keep valuable infra pieces **available** but **safe & cheap**. A small allowlist is permitted in MVP when guardrails are on. Everything else remains parked until promoted.

## Scope
- **Kubernetes-readiness & container hardening** (Dockerfile.production, .dockerignore, health probes).
- **Observability endpoints** (`/metrics`, Prometheus collectors) — *allowlisted* in MVP with guardrails.
- **Scaling status API** (`/scaling/status`) — *allowlisted* in MVP with guardrails.
- **OpenTelemetry expansion** (namespaces, conventions) — minimal, export disabled by default.
- **Non-FX domains (e.g., Blockchain ops)** — tracked but out of MVP.

## Out-of-scope for MVP
- Enabling these endpoints/collectors in RED environment.
- Any non-essential infra cost in Azure (extra pods, persistent scrape targets).

## Deliverables (Docs)
- Architecture notes (`DOCS/Architecture/FeatureFlags.md`): how to gate these.
- Config specimens with all features **disabled by default**.

## Risks if left unguarded
- **Cost creep** (extra containers, scrapes, storage).
- **Attack surface** (new unauth endpoints).
- **Scope drift** away from FX-first MVP.

## MVP Allowlist (with guardrails)
| Feature ID | Name | Allowed Condition | Guardrails | Cost Notes |
|-----------:|------|-------------------|------------|-----------|
| E8.F2 | Prometheus `/metrics` | **Monitoring.Enabled=true, Prometheus=true** | Bind to **loopback/private**; **admin auth**; **1m scrape**; no remote writes | No extra infra; tiny CPU if polled locally |
| E8.F3 | `/scaling/status` | **Scaling.Enabled=true, ExposeStatusEndpoint=true** | **Admin auth**; no PII/tenant data; redact counts | Zero if rarely hit |
| E8.F1 | Docker/K8s readiness | Always allowed (docs) | N/A | Docs-only; zero runtime |
| E8.F4 | OpenTelemetry minimal | **Monitoring.OpenTelemetry=true** | **Exporters disabled** by default; sampling low if turned on | Near-zero until exporters are configured |
| E8.F5 | Non-FX domains | Not allowed in MVP | Parked until WP raised | N/A |

## Admin Auth (what `RequireRole("Admin")` means)
`RequireRole("Admin")` is implemented via an **authorization policy** called `AdminOnly` that accepts **either**:
1) A **role claim** equal to `Admin` (any of: `role`, `roles`, or the .NET role URI), **or**
2) A valid **Admin API key** presented as `X-Admin-Token` (configurable **fallback** for RED/local only).

See: `DOCS/Security/AdminAuth.md` for the exact policy and code snippet.

**Guardrail expectation:** `/metrics` and `/scaling/status` **must** require `AdminOnly` when enabled.

## Cost Monitoring & Alerts (MVP defaults)
- Create a **Budget** on the resource group with alerts at **50% / 80% / 100%**.
- Add **Azure Monitor** alerts (or ACA/App Service equivalents):
  - **Egress data/day** > 2 GB (RED) → warn; > 5 GB → alert.
  - **Avg CPU (5 min)** > 70% sustained 15 min → scale/investigate.
  - **SignalR connections** > planned cap (e.g., 500) → investigate.
  - **Log volume** > 1 GB/day (RED) → sampling review.
See `DOCS/Ops/CostMonitoring.md` for step-by-step CLI and thresholds.

## Promotion Criteria — MVP‑optional → Core
All must pass for 7 days in GREEN (canary → full):
1) **Security:** `AdminOnly` enforced; no unauthenticated access observed; no high/critical findings.
2) **Perf:** p95 latency delta ≤ **+5%** vs baseline; zero hub disconnect spikes.
3) **Cost:** Monthly delta ≤ **£15** for enabled items; budgets/alerts clean for 7 days.
4) **Reliability:** SLO errors unchanged; no crash loops; health checks green.
5) **V&V:** Guardrail smoke tests (404/401/200) in CI; traceability matrix updated.
6) **Ops:** Runbook exists (enable/disable, rollback, dashboards).

## Blast Radius Limits & Kill Switches
- **Flags:** `Features.Monitoring.Enabled`, `Features.Monitoring.Prometheus`, `Features.Scaling.Enabled`, `Features.Scaling.ExposeStatusEndpoint` — flipping to `false` **removes routes**.
- **Binding:** `/metrics` bound to loopback/private address only in RED; **no public exposure**.
- **Auth:** `AdminOnly` policy required; least-privilege tokens/groups.
- **Rate limits:** recommend 10 rps per admin for `/metrics` and `/scaling/status` (see FeatureFlags doc).
- **Timeouts:** 2s route timeout to prevent slow-scan amplification.
- **No PII:** `/scaling/status` must redact tenant/user data.
- **Runbook:** rollback = disable flags → 404; optional recycle instance.

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E8.F1 | Prod Docker/K8s readiness | PLAN-E8-Docker-K8s | Docs-only; MVP allowed |
| E8.F2 | Prometheus metrics endpoint (guarded) | SMK-E8-Metrics-404;SMK-E8-Metrics-401;SMK-E8-Metrics-200 | MVP-allowed with guardrails |
| E8.F3 | Auto-scaling status endpoint (guarded) | SMK-E8-Scaling-404;SMK-E8-Scaling-401;SMK-E8-Scaling-200 | MVP-allowed with guardrails |
| E8.F4 | OpenTelemetry expansion | PLAN-E8-OTel | DOCS/OBSERVABILITY/OpenTelemetryPlan.md |
| E8.F5 | Non-FX domain stubs (Blockchain) | PLAN-E8-Blockchain | OTel plan mentions + commit refs |

### Acceptance (MVP allowlist)
- **Disabled** → endpoints return **404**; **Unauth** → **401**; **Enabled+Auth** → **200**.
- Bindings: metrics internal/private; scaling status behind admin auth and without PII.
- p95 latencies unaffected at low RPS; no persistent scrape/storage in RED.

### Rollback
- Keep flags **false**; remove deployment manifests from default pipelines.

## Notes
- Promotion to Core WPs requires: updated TraceabilityMatrix rows → smoke tests → cost check.

---
**Status**: MVP-optional (Guardrailed)
**Last Updated**: 2025-09-15
**Owner**: DevOps Team
**Next Phase**: Promotion to Core pending 7-day validation