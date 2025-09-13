# PatchOn — Sep 13, 2025

This folder holds day-stamped review artifacts and patch files produced by ChatGPT.

## Files

- `1958_WP1_Alignment.patch` — initial WP1 alignment patch (already applied).
- `2015_WP1.1.patch` — **follow-up actionables** (coverage gate workflow, PR checklist).

## Apply a patch

From repo root:

```bash
git apply REVIEW/CodeReview/ChatGPT/PatchOn/20250913/2015_WP1.1.patch
```

Or commit it as a change:

```bash
git add -A
git commit -m "review: WP1.1 actionables — coverage workflow + PR checklist + docs"
```

> Patches in this folder are additive and safe to re-run; they only create new files or tweak CI/docs in isolated locations.

---

## What this patch does

1. Adds a **coverage enforcement workflow** (`.github/workflows/ci-coverage.yml`) that:
   - Runs on `push`/`pull_request` for `master`, `main`, and `develop`.
   - Collects coverage via `XPlat Code Coverage`.
   - **Enforces a soft gate** (default min 60%) *if* a Cobertura report is present; otherwise logs and skips (to avoid blocking while coverage is being wired in WP1).
   - Uploads coverage artifacts for inspection.
2. Introduces a **Pull Request template** with checks for secrets, docs, tests, and WP linkage.
3. Adds this **README** so reviewers know how to apply/iterate future patches.

> Ratchet the coverage threshold upwards in later WPs. See `env: MIN_COVERAGE` in the workflow.