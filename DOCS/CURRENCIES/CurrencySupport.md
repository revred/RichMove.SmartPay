# Currency Support — Core Roster & Enablement

This document defines the currencies we treat as **first‑class** in the SmartPay MVP and how new ones are added with **minimal impact**.

## Tier‑1 (first‑class) currencies
We prioritize these for docs, examples, test coverage, and partner discussions:

- **GBP** — Pound Sterling (United Kingdom)
- **EUR** — Euro (Eurozone)
- **USD** — US Dollar (United States)
- **INR** — Indian Rupee (India)
- **AUD** — Australian Dollar (Australia) ← **added as Tier‑1 in this patch**
- **AED** — UAE Dirham (United Arab Emirates) — used in examples for the AUD↔AED corridor

> Note: The UAE currency code is **AED**, not "UAE". We use ISO‑4217 codes throughout.

## Decoupled design stance
- **Core & API** accept any ISO‑4217 3‑letter code (`^[A-Z]{3}$`). Adding a currency **does not require code changes**.
- Supported/marketed currencies are declared in **docs & config**, not hardcoded. Validation beyond ISO format is performed at the **provider layer** and contracts.
- Feature behavior (fees, rounding, corridors) is **config‑driven** and covered by **contract tests** + **examples**, not new classes.

## Minimal enablement checklist (per new currency)
1. Add to docs roster (this file) + update examples
2. Add corridor examples (request/response) under `DOCS/API/examples`
3. Add Postman request(s) for quick verification
4. Add contract tests that validate example payloads (structure, ISO codes, required fields)
5. (Optional) Add pricing/rounding goldens if fees differ
6. (Optional) Add `SupportedCurrencies.sample.json` for environments that want explicit allowlists

## Corridors & examples
Typical Tier‑1 corridors we will demonstrate and test:

- GBP→EUR, GBP→USD, GBP→INR
- **AUD→AED** (new example), AUD→USD, AUD→EUR

> These examples are indicative; the Null/Dev providers return synthetic data. Production FX providers will drive availability & quotes per corridor.

## Config allowlist (optional)
Some deployments may require a strict allowlist. This patch adds a sample at `DOCS/CONFIG/SupportedCurrencies.sample.json` (not wired by default).

## FAQ
**Q: Do we need to change Core for a new currency?**
A: No. Core uses ISO‑4217 strings; no hardcoded enums.

**Q: Why include AED with AUD?**
A: The requested corridor mentioned "UAE"; the ISO currency is **AED**. We include examples to show the path for Middle‑East corridors.