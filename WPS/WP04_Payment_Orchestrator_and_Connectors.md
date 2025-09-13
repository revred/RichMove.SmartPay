# WP4 — Payment Orchestrator & Connectors

## Features
- Orchestrator with provider adapters (Stripe, TrueLayer, PayPal).
- Unified events model; signature verification.

## Tasks
- Adapter interfaces; mock + sandbox implementations.
- Stripe: intent create/capture/refund; disputes webhooks.
- TrueLayer: A2A init/consent/result; webhooks.
- PayPal: order create/capture; webhooks.
- Outbox→webhook dispatcher; retries with backoff.

## Commit Points
- `feat(wp4): orchestrator interfaces + mocks`
- `feat(wp4): stripe adapter + tests`
- `feat(wp4): truelayer adapter + tests`
- `feat(wp4): paypal adapter + tests`

## Regression
- Sandbox flows E2E; duplicate webhook delivery remains idempotent.

## DoD
- All connectors pass contract tests; unified event types emitted.
