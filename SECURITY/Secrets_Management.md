# Secrets Management

- Local: use `.env` (never commit). Template: `.env.example`.
- CI: GitHub Actions **encrypted secrets** and OIDC where possible.
- Supabase Service Role Key: store as a GitHub environment secret; restrict by environment (dev/stage/prod).
- Optional: Use **SOPS + age** if you need to commit encrypted config files. Keep private keys **out of the repo**.
- Pre-commit: run `gitleaks` to scan for accidental secrets.
