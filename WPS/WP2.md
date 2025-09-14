# WP2 — Foreign Exchange Core

## Scope
- `POST /api/fx/quote` happy path + validation, persistence, DB health.

## Deliverables
- Endpoint, storage path, background pricing tick.

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E2.F1 | Create FX Quote | SMK-E2-Quote-OK, SMK-E2-Quote-400 | Smoke_Features.md §3.2-A/B |
| E2.F2 | Persist quotes | INTEG-E2-DB-Save | Integration tests (DB) |
| E2.F3 | Pricing background | OBS-E2-Metrics | Logs/metrics (optional PR) |
| E2.F4 | DB health | SMK-E2-DBHealth | Smoke_Features.md §3.2-C |
| E2.F5 | Fallback mode | SMK-E2-NoDB (nightly) | Nightly matrix |

### Acceptance
- Valid request returns JSON with id/rate; invalid returns RFC7807; DB health 200.

### Rollback
- Disable persistence via config for local or degraded modes.