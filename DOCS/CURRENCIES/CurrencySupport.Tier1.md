# Tier‑1 Currencies (MVP)

We treat the following ISO‑4217 currencies as **Tier‑1** (front‑row documentation, examples, tests):

- **GBP** — Pound Sterling
- **EUR** — Euro
- **USD** — US Dollar
- **INR** — Indian Rupee
- **AUD** — Australian Dollar  ← **added**
- **AED** — UAE Dirham (corridor peer)

**Design stance:** Contracts are generic (3‑letter ISO codes). Adding a currency is a documentation + config + test task, not a Core refactor.

**Common corridors we showcase:**
- GBP→EUR, GBP→USD, GBP→INR
- **AUD→EUR**, **AUD→AED**, AUD→USD