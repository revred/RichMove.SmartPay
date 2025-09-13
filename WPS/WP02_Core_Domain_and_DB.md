# WP2 — Core Domain & DB

## Features
- Supabase schemas (idn, pay, ops, ref), RLS, outbox/audit.
- Entities & repositories with EF Core adapters.

## Tasks
- Implement schema & RLS migrations.
- Domain models (Customer, Quote, Payment, WebhookEndpoint).
- Outbox dispatcher skeleton; audit appender.

## Commit Points
- `feat(wp2): schema + RLS + domain models`
- `feat(wp2): outbox + audit scaffolds`

## Regression
- Integration tests: RLS isolation; idempotent insertions.

## DoD
- Tables created; RLS active; tests green.
