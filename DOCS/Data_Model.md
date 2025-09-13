# Data Model (Core)

- Customer (id, user_id, kyc_status, risk_rating, created_at)
- Quote (pair, base_amount, rate, spread_bps, provider, expires_at)
- Payment (status, provider, refs, amounts, currencies)
- WebhookEndpoint (url, secret, active)
- Outbox (event_type, payload, attempts, schedule)
- Audit (actor, action, entity, meta)
