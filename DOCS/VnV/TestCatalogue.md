# Test Catalogue (IDs and Locations)

Map of **Test IDs** to **files/commands** used across PR and nightly matrices.

## Smoke (SMK-*)
- **SMK-E1-Host** — `curl -I /` (Smoke_Features.md §3.1-A)
- **SMK-E1-Swagger** — `curl /swagger/index.html` (Smoke_Features.md §3.1-B)
- **SMK-E1-Health** — `curl /health/*` (Smoke_Features.md §3.1-C)
- **SMK-E2-Quote-OK** — POST `/api/fx/quote` valid (Smoke_Features.md §3.2-A)
- **SMK-E2-Quote-400** — POST invalid (Smoke_Features.md §3.2-B)
- **SMK-E2-DBHealth** — `GET /v1/health/db` (Smoke_Features.md §3.2-C)
- **SMK-E4-Hub-Negotiate** — POST `/hubs/notifications/negotiate` (Smoke_Features.md §3.3-A)
- **SMK-E4-Trigger-RoundTrip** — listen + POST quote (Smoke_Features.md §3.3-C)
- **SMK-WP5-Webhook-Delivery** — webhook endpoint receives POST (config driven)

## Unit (UNIT-*)
- **UNIT-E4-TenantResolver** — `ZEN/TESTS/WP4/TenantResolverTests.cs`
- **UNIT-WP5-Signer** — `ZEN/TESTS/Api.Tests/WebhookSignerTests.cs`

## Integration (INTEG-*)
- **INTEG-E2-DB-Save** — Integration test harness (DB)
- **INTEG-WP5-RLS** — DB behavior with/without service role

## Performance (PERF-*)
- **PERF-E4-K6-Quote** — `ANALYSIS/PERF/scenarios/fx-quote-smoke.js`

## Observability (OBS-*)
- **OBS-E2-Metrics** — logs/metrics show pricing tick
- **OBS-WP5-Outbox** — logs show retries/backoff