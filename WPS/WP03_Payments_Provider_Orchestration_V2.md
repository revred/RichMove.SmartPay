# WP3 — Payments (Minimal Provider) — V&V Alignment

This document aligns the already-implemented minimal provider with crisp **idempotency semantics**, **error model**, and **smoke coverage**.

## Scope
- `POST /api/payments/intent` (Mock provider).
- `POST /api/payments/webhook/mock` with `MockPay-Signature` (HMAC-SHA256 of raw body).
- Idempotency: `Idempotency-Key` header, TTL 24h; duplicate → `Idempotent-Replay: true` response header.

## Error Model
| HTTP | Code | When | Notes |
|---:|---|---|---|
| 400 | invalid_request | Amount <= 0, currency missing/invalid | MVP behavior |
| 401 | unauthorized | (Future) if endpoint secured | Currently open for demo |
| 409 | idempotency_conflict | Reserved for strict semantics | Not used in MVP |

## V&V (Traceability)
| Feature ID | Name | Test IDs | Evidence |
|-----------:|------|----------|---------|
| E3.F1 | Create payment intent | SMK-E3-CreateIntent | `Smoke/payments.intent.http` |
| E3.F1.N1 | Idempotency replay marker | SMK-E3-IdemReplay | `Smoke/payments.intent.http` (repeat) |
| E3.F2 | Webhook signature verify | SMK-E3-MockWebhook | `Smoke/payments.mock.webhook.http` |

## Acceptance
- Replay requests do not create duplicates and include the `Idempotent-Replay` header.
- Webhook rejects invalid signature.
- Provider returns `requires_confirmation` and a `clientSecret` for demo.

## Smoke
```http
# Create intent
POST http://localhost:5001/api/payments/intent
Content-Type: application/json
Idempotency-Key: idem-001

{ "currency":"GBP", "amount":100.00, "reference":"ORDER-001" }

### Replay (expect Idempotent-Replay header)
POST http://localhost:5001/api/payments/intent
Content-Type: application/json
Idempotency-Key: idem-001

{ "currency":"GBP", "amount":100.00, "reference":"ORDER-001" }
```

```http
# Mock webhook (sign body and place hex in header)
POST http://localhost:5001/api/payments/webhook/mock
Content-Type: application/json
MockPay-Signature: <hex hmacsha256(body, secret)>

{ "type":"payment_intent.succeeded", "intentId":"mpi_123", "tenantId":"default" }
```