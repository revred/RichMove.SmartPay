# WP1 — Platform Foundation

## Scope
- API shell (FastEndpoints/minimal hosting), Swagger, health checks, CI coverage gate.

## Deliverables
- `/swagger/*`, `/health/live`, `/health/ready`, CI job with coverage ≥60%.

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E1.F1 | API shell alive | SMK-E1-Host | Smoke_Features.md §3.1-A |
| E1.F2 | Swagger available | SMK-E1-Swagger | Smoke_Features.md §3.1-B |
| E1.F3 | Health live/ready | SMK-E1-Health | Smoke_Features.md §3.1-C |
| E1.F4 | Coverage gate | CI-E1-Coverage | CI logs |

### Acceptance
- 200 on `/health/*`; Swagger 200; coverage ≥60%.

### Rollback
- None required; non-breaking.