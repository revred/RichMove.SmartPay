# Threat Model (STRIDE)

## Data Flows
- Client -> API -> Providers (Stripe/TrueLayer/PayPal) -> Webhooks
- API -> Supabase (DB, Storage)

## Threats & Controls
- **Spoofing**: JWT auth, webhook HMAC, TLS.
- **Tampering**: Idempotency keys, outbox signatures, append-only audit.
- **Repudiation**: Correlated audit logs, time-synced.
- **Information Disclosure**: RLS, field-level redaction, encryption in transit/at rest.
- **Denial of Service**: rate limits, circuit breakers.
- **Elevation of Privilege**: RBAC, least privilege DB roles.
