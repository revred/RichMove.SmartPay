# WP5 — FX & Remittance (SPI)

## Features
- Quote engine (mid + spread), provider routing, expiry.
- Remittance payout flow (paper mode; partner live later).
- Ledger entries; reconciliation job.

## Tasks
- Implement /fx/quote; snapshot into orders.
- Routing table (ref.fx_routes); configurable spreads.
- Payout API: default PSP settlement + SPI remittance path.
- Recon job with pg_cron; reporting tables.

## Commit Points
- `feat(wp5): fx quote engine + tests`
- `feat(wp5): payouts + recon`

## Regression
- Precision/rounding across currencies; quote expiry honored.
- Reconciliation balanced daily.

## DoD
- Quotes stable; payouts simulated; recon reports consistent.
