# WP10 — Quality, CI/CD & Regression

## Features
- 99% coverage gates; mutation ≥95%; contract/load integrated.
- Chaos profile; nightly jobs.

## Tasks
- Coverlet + ReportGenerator gate; Stryker threshold.
- Schemathesis + Dredd; k6 smoke & nightly longer.
- Chaos toggles in config.

## Commit Points
- `chore(ci): enforce coverage & mutation`
- `test(contract): schemathesis`
- `test(load): k6 smoke`

## Regression
- CI fails under threshold; artifacts stored.

## DoD
- All gates active; dashboards available.
