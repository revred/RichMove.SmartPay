# WP1 — Repository & Tooling

## Features
- Solution scaffolding, coding standards, analyzers.
- CI gates (coverage, mutation, contract, load).
- PR templates, commit policy, release checklist.

## Tasks
- Create solution & projects; add analyzers.
- Wire GitHub Actions workflows.
- Add Stryker config; Schemathesis; k6 smoke.
- Add templates (PR, issue) and CODEOWNERS.

## Commit Points
- `feat(wp1): solution + analyzers`
- `chore(ci): coverage & mutation gates`
- `chore(test): contract+load harness`

## Regression
- CI must pass on empty API; coverage gate active.

## DoD
- CI green; coverage gate enforces ≥99%; mutation wired.
