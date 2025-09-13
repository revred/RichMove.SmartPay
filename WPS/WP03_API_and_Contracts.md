# WP3 — API & Contracts

## Features
- Minimal APIs; OpenAPI 3.1; SDK stubs (C#/JS).

## Tasks
- Endpoints: /customers, /quotes, /payments, /webhooks.
- Idempotency middleware; error model; problem+json.
- OpenAPI annotations; generate clients.

## Commit Points
- `feat(wp3): minimal api + idempotency`
- `docs(api): openapi + sdk stubs`

## Regression
- Contract tests green; duplicate POST returns same result.

## DoD
- OpenAPI published; SDK builds; Schemathesis passes.
