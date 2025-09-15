# Admin Auth (`AdminOnly`) — Semantics

Grant if **role=Admin** or header `X-Admin-Token` matches configured secret (RED only).
Endpoints: `/metrics` and `/scaling/status` additionally require feature flags On.
When flags Off → return 404.

## Verification
- Flags Off → 404
- Flags On + no auth → 401/403
- Flags On + Admin → 200

## Rate limits
- `/metrics`: 10 rps, `/scaling/status`: 5 rps