# Git Strategy

- **Default branch**: `main`
- **Feature branches**: `feature/<wp>-<short-name>` (e.g., `feature/wp4-orchestrator`)
- **Commit cadence**: Logical commits per task; end-of-task commit message format:
  - `feat(wp4): implemented Stripe intent create [tests]`
- **PR template**: Include: scope, tests, coverage %, mutation %, contracts & k6 summary, security notes.
- **Release tags**: `v0.x.y` (semver). Nightly snapshots allowed on `develop` (optional).
