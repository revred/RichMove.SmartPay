# Admin SSR — Consumption Plan (WP6)

**Goal:** Keep costs near-zero while providing a minimal admin.

## Data flows
- Health → GET `/health/ready`
- FX quote demo → POST `/api/fx/quote`

## Hosting
- Azure App Service (B1) or Container Apps min scale; scale-to-zero when idle.

## Notes
- SSR avoids bundling; no SPA downloads.