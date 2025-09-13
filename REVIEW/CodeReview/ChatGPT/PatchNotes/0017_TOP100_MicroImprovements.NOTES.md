# Patch Notes — 0017 Top‑100 Micro Improvements

This patch **adds docs, analyzer configs (as samples), CI, and helper classes** to make it easy to adopt 100 small improvements—without rewriting existing code.

## What's added
- **Top‑100 list**: actionable, low-risk tasks you can land piecemeal.
- **Read‑only & allocations guide**: clear standards for immutability and low‑alloc patterns.
- **Analyzer config (sample)**: `.editorconfig.smartpay.additions` + `Directory.Build.props.sample` to opt-in analyzers.
- **CI**: `analyzers` workflow with format check and Roslynator (advisory).
- **Core helpers**: `Ensure`, `Patterns` (GeneratedRegex), and `Log` (LoggerMessage).
- **Perf playbook** and **headers playbook** for ops/devs.

## Why this is safe
- No existing files are modified; everything is additive.
- Analyzer rules are **suggestions** until you opt-in by renaming `Directory.Build.props.sample`.
- Helper classes are inert until referenced.

## Adoption path
1. Land patch → review docs with devs.
2. Enable analyzers by copying `ANALYZERS/Directory.Build.props.sample` to repo root as `Directory.Build.props`.
3. Start using `Ensure`, `Patterns`, and `Log` in touched files.
4. Track progress with the Top‑100 list; check off items per PR.

— ChatGPT