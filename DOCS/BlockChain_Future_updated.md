# Blockchain Future — Strategy, Milestones, and Minimal USP Wiring
_Last updated: 2025-09-13 (UTC)_

This document extends our organic, additive blockchain plan with a **minimal, isolated wiring** so we can
*show* blockchain capability (our USP) **without increasing MVP complexity**. The code paths are **feature-flagged**
and live in a separate vertical slice (`Blockchain/*` endpoints + repositories). If the feature flag is off, nothing changes.

> **Key takeaway:** With the included patch, the API exposes **3 opt‑in endpoints** (wallet creation, on‑chain intent,
> and transaction ingestion) that write to the blockchain-ready tables. If the flag is disabled, endpoints return a clear “disabled” response.  
> The **database is already capable** of storing blockchain information; now the API has thin, isolated routes to use it when you opt in.

---

## Feature flag
Set `Blockchain__Enabled=true` to enable the endpoints. Keep `false` for MVP.

- Requires your Postgres (Supabase) connection to be enabled (we reuse the existing `NpgsqlDataSource`).

### Endpoints (FastEndpoints, isolated)
- `POST /v1/chain/wallets` → create a wallet record for a user (custodial or external).
- `POST /v1/chain/intents/onchain` → create a chain-agnostic payment intent with `route='ONCHAIN'`.
- `POST /v1/chain/tx/ingest` → ingest an observed on-chain transaction; optionally link it to an intent by creating
  a settlement and legs (credit/fee) in one go.

Each endpoint is designed to be **idempotent-ish** (e.g., `onchain_tx` uniqueness on `(chain_id, tx_hash)`), and—crucially—**does not change any existing routes**.

---

## Minimal data flow (when enabled)
1. Create/record wallets (`/v1/chain/wallets`).
2. Create a payment intent (`/v1/chain/intents/onchain`) with `source_asset_id`, `target_asset_id`, `amount_source`.
3. When your indexer/webhook sees a transaction, call `/v1/chain/tx/ingest` → records `onchain_tx` and, if `intent_id` is provided, creates a `settlement` and `settlement_leg` rows tied to that tx.

This flow is **optional** and can be left unused until you need it.

---

## What the patch does (recap)
- Adds 3 new endpoints in a `Blockchain` area (completely separate from existing controllers).
- Adds thin repositories that write into the `0003_blockchain_prep.sql` tables.
- Adds a small feature flag section to `.env.example` (`Blockchain__Enabled=false` by default).
- Updates this document to make the strategy and wiring explicit.

> **Critical detail (do not lose this):** The **DB is capable of storing blockchain information now**.  
> These endpoints simply make use of that capability when you flip the flag.

---

## Table motivations (from the prep doc, condensed)
- **chain_network**: normalized networks (e.g., EVM mainnet/testnet), keeps chain details out of payments.
- **asset**: unifies fiat and tokens; avoids crypto leaking into core payments; unique per `(code, chain, contract)`.
- **wallet**: on-chain addresses by user/chain; supports EXTERNAL or CUSTODIAL.
- **payment_intent**: describes *what* the user wants; route “ONCHAIN” vs “OFFCHAIN” is a switch, not a forked model.
- **settlement / settlement_leg**: represents *how* it executed (multi-leg, fees, credits/debits).
- **onchain_tx**: append-only envelope for observed transactions; unique `(chain_id, tx_hash)`.

---

## Safety & simplicity
- Feature-flagged, isolated endpoints.
- Uses the same Postgres pool (`NpgsqlDataSource`), no new infra.
- Optional linking to intents; you can ingest tx without an intent first, then link via a settlement later.
