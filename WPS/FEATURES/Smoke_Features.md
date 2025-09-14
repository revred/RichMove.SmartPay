# Smoke_Features.md
_Last updated: 2025-09-14 04:00–09:45 (WP4, WP4.1, WP6 docs plan)_

> **Purpose:** A **single living document** to enumerate product capabilities as a tree (Epics → Features → Nuggets → Requirements) and specify **minimal smoke tests** per item. The goal is **zero bloat**: every line of code maps to a user-visible benefit or a hardening/ops capability, and every item has a fast probe to prevent silent regressions.

---

## 0) How to use this document
- Treat this as the **source of truth** for smokable capabilities. Update after every Work Package (WPx).
- Pair it with the CSV inventory (**SmartPay_Feature_Inventory.csv**) used by CI to auto-generate smoke probes.
- Run probes locally before pushing; CI runs a **quick matrix** on PRs and a **full matrix** nightly.

### Required env (typical local)
```
SMARTPAY_BASE_URL=http://localhost:5001
SMARTPAY_TENANT=default
SMARTPAY_API_KEY=<if used>
```
> Some probes require Supabase/local DB; others are HTTP-only.

---

## 1) Serialization scheme
- **Epic**: `E#` (e.g., `E4` Advanced Features)
- **Feature**: `E#.F#` (e.g., `E4.F5` FX Quote Created Trigger)
- **Nugget** (small enabling snippet): `E#.F#.N#`
- **Requirement/Expectation**: `E#.F#.R#` *(optional at any level)*

Example: `E4.F3.N1.R1` — a requirement for nugget N1 in feature F3 under epic E4.

Status legend: ✅ implemented · 🟡 planned/stub · ⏳ deferred · ❌ removed

---

## 2) Feature Tree (WP1 → WP4.1, WP6 plan)
Below we list the tree with **status** and **smoke intent**. Concrete commands live in §3.

### E1 — Platform Foundation (✅)
- **E1.F1** FastEndpoints API shell — _✅_
  - **Smoke:** Framework routes respond and hosting model is alive (see §3.1-A).
- **E1.F2** Dual Swagger/OpenAPI UIs — _✅_
  - **Smoke:** `GET /swagger` returns 200/OK (see §3.1-B).
- **E1.F3** Health checks — _✅_
  - **Smoke:** `GET /health/live` & `/health/ready` return 200 (see §3.1-C).
- **E1.F4** CI with coverage gate — _✅_
  - **Smoke:** `dotnet test` runs; (coverage threshold verified in CI).

**E1 Requirements**
- **E1.R1** p95 API TTFB < 300 ms (tracked by perf smoke; §3.4-A).

---

### E2 — Foreign Exchange Core (✅)
- **E2.F1** Create FX Quote — _✅_
  - **Smoke:** `POST /api/fx/quote` with valid payload → 200/201 + JSON fields (see §3.2-A).
  - **Negative:** invalid currency → 400 (see §3.2-B).
- **E2.F2** Persist quotes (Supabase) — _✅_
  - **Smoke:** response contains an id; persistence is exercised in integration tests.
- **E2.F3** Dynamic pricing background — _✅_
  - **Smoke:** optional (observed via metrics/logs; non-blocking in PR).
- **E2.F4** DB health — _✅_
  - **Smoke:** `GET /v1/health/db` returns 200 (see §3.2-C).
- **E2.F5** Fallback (No-DB) mode — _✅_
  - **Smoke:** optional; covered by config matrix in nightly runs.

**E2 Requirements**
- **E2.R1** Quote precision 4 dp; currency whitelist enforced.

---

### E3 — Payment Provider Orchestration (🟡 planned)
- **E3.F1** Multi-provider routing — _🟡_
- **E3.F2** Provider health/SLA scoring — _🟡_

**E3 Requirements**
- **E3.R1** Graceful degradation to best available provider.

---

### E4 — Advanced (WP4 + WP4.1) (✅)
- **E4.F1** SignalR Notifications Hub (`/hubs/notifications`) — _✅_
  - **Smoke:** negotiate endpoint returns non-404 (see §3.3-A).
- **E4.F2** Notification service (In-Memory) — _✅_
  - **Smoke:** see trigger flow under **E4.F5**.
  - **E4.F2.N1** Supabase Realtime (stub) — _🟡_
- **E4.F3** Multi-tenancy middleware (Host/Header) — _✅_
  - **Smoke:** request with `X-Tenant` header returns 2xx on a public-safe endpoint (§3.3-B).
  - **E4.F3.N1** Tenant resolver strategies — _✅_
- **E4.F4** Lightweight analytics — _✅_
  - **Smoke:** presence of structured log line and counters/histogram increment (optional in PR).
- **E4.F5** FX Quote Created Trigger — _✅_
  - **Smoke (end-to-end):**
    1) Start a simple SignalR client (see §3.3-C).  
    2) `POST /api/fx/quote` → expect `fx.quote.created` payload within 5s.
- **E4.F6** Perf scenarios (k6) — _✅_
  - **Smoke:** `k6 run ANALYSIS/PERF/scenarios/fx-quote-smoke.js` p95 under guardrail (§3.4-A).
- **E4.F7** Hub negotiation available — _✅_
  - **Smoke:** duplicates **E4.F1** check; keep a single probe in matrix.

**E4 Requirements**
- **E4.R1** Realtime is opt-in per page; reconnect backoff ≤ 30 s.

---

### E5 — Security & Secret Hygiene (✅)
- **E5.F1** Security policy — _✅_
- **E5.F2** gitleaks config — _✅_
  - **Smoke:** `gitleaks detect` returns no findings in PR (CI-only).

---

### E6 — DevEx & Observability (✅)
- **E6.F1** WPS documentation — _✅_
- **E6.F2** Structured request logging — _✅_

---

### E7 — UI & SDK (WP6) (🟡 plan)
- **E7.F1** Blazor SSR Admin UI — _🟡_
- **E7.F2** OpenAPI-first SDKs (C#/TS) — _🟡_
- **E7.F3** Webhooks + verifiers — _🟡_

---

### E8 — Hosting & Cost (🟡 plan)
- **E8.F1** ACA/App Service + Azure SignalR minimal footprint — _🟡_
- **E8.F2** Output caching for stable GETs — _🟡_

---

## 3) Smoke probes (commands & expected outcomes)

> **Base URL** in examples assumes: `${SMARTPAY_BASE_URL}` (default `http://localhost:5001`).  
> **Tenant** header: `X-Tenant: ${SMARTPAY_TENANT}` (default `default`).

### 3.1 Platform
**A) Hosting alive (coarse):**
```bash
curl -I ${SMARTPAY_BASE_URL}/ || true
# Expect a valid HTTP response (200–404). Any TCP failure = 🔴
```

**B) Swagger UI:**
```bash
curl -s -o /dev/null -w "%{http_code}\n" ${SMARTPAY_BASE_URL}/swagger/index.html
# Expect: 200
```

**C) Health:**
```bash
curl -s -o /dev/null -w "%{http_code}\n" ${SMARTPAY_BASE_URL}/health/live
curl -s -o /dev/null -w "%{http_code}\n" ${SMARTPAY_BASE_URL}/health/ready
# Expect: 200 and a minimal JSON body if applicable
```

### 3.2 FX Core
**A) Create quote (happy path):**
```bash
curl -s -X POST "${SMARTPAY_BASE_URL}/api/fx/quote"   -H "Content-Type: application/json"   -H "X-Tenant: ${SMARTPAY_TENANT}"   -d '{"fromCurrency":"USD","toCurrency":"GBP","amount":1000}' | jq .
# Expect: JSON with id/quoteId, fromCurrency, toCurrency, amount, rate
```

**B) Create quote (validation failure):**
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST "${SMARTPAY_BASE_URL}/api/fx/quote"   -H "Content-Type: application/json"   -H "X-Tenant: ${SMARTPAY_TENANT}"   -d '{"fromCurrency":"XXX","toCurrency":"GBP","amount":-5}'
# Expect: 400 with ProblemDetails JSON
```

**C) DB health:**
```bash
curl -s -o /dev/null -w "%{http_code}\n" ${SMARTPAY_BASE_URL}/v1/health/db
# Expect: 200 (or 503 if intentionally degraded)
```

### 3.3 Realtime & Tenancy
**A) SignalR negotiate:**
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST   "${SMARTPAY_BASE_URL}/hubs/notifications/negotiate?negotiateVersion=1"
# Expect: not 404 (typically 200/201/204)
```

**B) Tenancy (header strategy path):**
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X GET   "${SMARTPAY_BASE_URL}/health/live" -H "X-Tenant: ${SMARTPAY_TENANT}"
# Expect: 200. Use logs to verify tenant flow if needed.
```

**C) Trigger round-trip (listen + fire):**
_Pseudocode outline_
1. Start a tiny client (Node/C#) that connects to `${BASE}/hubs/notifications`, then subscribes to `fx.quote.created`.
2. In another shell, run the **Create quote** command (§3.2-A).
3. Expect a payload like:
```json
{ "quoteId":"...", "fromCurrency":"USD", "toCurrency":"GBP", "amount":1000, "rate":1.23, "timestampUtc":"..." }
```

_Node snippet (illustrative):_
```js
// requires @microsoft/signalr
const hub = new signalR.HubConnectionBuilder()
  .withUrl(process.env.SMARTPAY_BASE_URL + "/hubs/notifications")
  .withAutomaticReconnect().build();
hub.on("fx.quote.created", msg => { console.log("EVENT:", msg); process.exit(0); });
await hub.start();
// Now POST /api/fx/quote in another shell; exit(0) on first event or timeout in 5s.
```

### 3.4 Performance (smoke)
**A) k6 quick scenario** (already in repo under `ANALYSIS/PERF/scenarios/fx-quote-smoke.js`):
```bash
BASE_URL=${SMARTPAY_BASE_URL} TENANT=${SMARTPAY_TENANT} k6 run ANALYSIS/PERF/scenarios/fx-quote-smoke.js
# Guardrail: p95 < 300ms (checked by k6 thresholds if configured)
```

---

## 4) Test matrix & categories
- **Category: User-facing (UF)** — endpoints & UX-affecting signals.
- **Category: Under-the-hood (UH)** — middleware, metrics, logs, background workers.
- **Runtime vs Build-time** — some checks (gitleaks, coverage) are CI-only.

**Recommended PR matrix (fast):**
- E1.F2, E1.F3, E2.F1 (happy + one negative), E4.F1, E4.F5 (event round-trip).
- Optional in PR: E4 analytics counters & logs.

**Recommended Nightly (full):**
- All PR items + E2.F5 fallback mode, E2.F3 background tick, E5 gitleaks, k6 perf probe.

---

## 5) Gap tracking & anti-bloat
- Every new code path must register here with an **ID** and a **smoke probe**.
- If a feature cannot be smoked, justify why (pure refactor, dead code slated for removal, etc.).
- Remove IDs when features are deleted; update CSV and this doc together.

---

## 6) Pointers
- Feature Inventory CSV: `SmartPay_Feature_Inventory.csv` (kept alongside this file; import into CI).

---

### Change Log
- **2025-09-14**: Initial version covering WP1, WP2, WP4, WP4.1 and WP6 plan.
