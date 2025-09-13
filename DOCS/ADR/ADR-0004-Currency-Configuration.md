# ADR-0004 — Currency Configuration & Expansion

## Status
Accepted (WP3)

## Context
We need to support new ISO‑4217 currencies (e.g., **AUD**) without refactoring Core or coupling API behavior to specific currencies.

## Decision
- Use **ISO‑4217 strings** (`^[A-Z]{3}$`) across Core and API (no enums).
- Keep **schemas** generic; use **examples** and **docs** to highlight Tier‑1 currencies.
- Provide an optional **config allowlist** (`SupportedCurrencies.sample.json`) for deployments that need governance. This is **not** wired by default.
- Cover new corridors via **examples**, **Postman**, and **contract tests** that validate example shape.

## Consequences
- Minimal code churn for currency additions.
- Provider layer remains responsible for real availability, pricing, and rounding rules.
- Contract tests guard against accidental schema drift.