# RichMove.SmartPay — Work Packages, Stage Gates & Regulatory Plan

This repository contains the **execution scaffold** to deliver RichMove.SmartPay to:
- **80% Shopify-equivalent** (storefront integrations, checkout orchestration, subscriptions, marketplaces, tax/shipping via partners), and
- **low-cost FX remittance via SPI** (no heavy regulatory overhead), 
with **20% effort** focus via strict scope and partner-led regulated flows.

**Date:** 2025-09-13

## Contents
- `PLAN/` — Master plan, work packages, timeline (Gantt), stage gates, DoD, comms.
- `WPS/` — Detailed Work Packages (features, tasks, commit points, regression tests).
- `REGULATORY/` — SPI application & partner onboarding checklists; document templates.
- `DOCS/` — Architecture, API skeleton, diagrams.
- `ZEN/` — **All source code and build files** (clean development environment).
  - `SOURCE/` — Core, Infrastructure, and API projects (.NET 9).
  - `TESTS/` — Unit, integration, and load test projects.
  - Build files: `*.sln`, `Directory.*.props`, `global.json`, etc.
- `COMMERCIAL/` — Pricing & unit-economics, partner & investor one-pagers.
- `GANTT/` — Mermaid + CSV task plan for import into PM tools.
- `SCRIPTS/` — Helpers (branch creation, release checklist).

> **Operating model**: Orchestration-first. Cards/A2A/wallets/escrow via regulated partners; RichMove focuses on API, UX, analytics, **FX + remittance** under SPI.

## New Additions (Repo Hygiene & Security)
- `.gitignore`, `.gitattributes`, `.editorconfig`
- Secrets handling: `.env.example`, `.envrc`, `SECURITY/Secrets_Management.md`, gitleaks scanning in CI
- `Makefile` shortcuts; `secret-scan` workflow
- Threat model and DPIA templates
