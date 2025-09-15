# Payments API (WP3 Minimal)

## Create Intent
`POST /api/payments/intent`

Headers:
- `Idempotency-Key` (optional). If omitted, server supplies one in response header.

Body:
```json
{ "currency":"GBP", "amount": 100.00, "reference":"ORDER123" }
```

Response 200:
```json
{ "provider":"MockPay", "intentId":"mpi_abc123...", "status":"requires_confirmation", "clientSecret":"..." }
```

Replay behavior: response header `Idempotent-Replay: true` when a duplicate key within 24h.

## Webhook — MockPay
`POST /api/payments/webhook/mock`

Header:
- `MockPay-Signature: <hex hmacsha256(payload, secret)>`

Body:
```json
{ "type":"payment_intent.succeeded", "intentId":"mpi_...", "tenantId":"default" }
```

On success, emits realtime topic `payment.intent.succeeded`.

## Config (samples)
`ZEN/SOURCE/Api/appsettings.WP3.sample.json`

```jsonc
{ "WP3": { "MockPay": { "Secret": "replace-me-mockpay" } } }
```