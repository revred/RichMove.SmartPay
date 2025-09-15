# Smoke Test Features

This file tracks features that have smoke tests for automated V&V validation.

## WP3 - Payment Provider
- [x] POST /api/payments/intent (create payment intent)
- [x] POST /api/payments/webhook/mock (webhook processing)
- [x] Idempotency key handling

## WP5 - Webhooks
- [x] Webhook outbox queuing
- [x] HMAC signature verification
- [x] Retry logic with backoff

## WP6 - Admin & SDK
- [x] Admin dashboard health checks
- [x] FX quote creation
- [x] SDK generation documentation

## WP7 - Operational Guardrails
- [x] AdminOnly policy enforcement
- [x] Feature flag-controlled endpoints
- [x] Rate limiting on admin endpoints
- [x] Metrics endpoint (/metrics)
- [x] Scaling status endpoint (/scaling/status)

## CI/V&V Gates
- [x] Coverage threshold (>=60%)
- [x] Feature flags disabled by default
- [x] V&V artifacts present (TraceabilityMatrix.csv, WPS/INDEX.md)
- [x] Automated test execution