# Top 100 Small-but-Impactful Improvements (WP1+WP2+WP3 complete, Blockchain optional, Gulf corridors, AUD Tier‑1)

> Philosophy: avoid rewrites. Prefer **low-risk, incremental** changes that improve clarity, correctness, performance, security, and DX.
> Everything here can be landed piecemeal. Many items are docs/config/CI only.

## A. API correctness & surface hygiene
1. **Use explicit `StringComparison`** in all string comparisons (prefer `Ordinal` / `OrdinalIgnoreCase`).
2. **Normalize header names** to constants (e.g., `X-Correlation-Id`, `Idempotency-Key` in one place).
3. **Fail closed on unknown JSON fields** (keep `additionalProperties: false` in schemas; ensure API rejects unexpected members).
4. **Pagination invariants** documented and enforced: cap page size, stable sort keys, `Link` headers (`next`, `prev`).
5. **RFC7807 everywhere**: ensure even validation failures return ProblemDetails (status + traceId).
6. **Idempotency TTL** doc states 24h; add config key (`Idempotency:Hours=24`) for environments.
7. **Correlation ID propagation** to outbound calls (providers) via a single delegating handler.
8. **Explicit DTO immutability**: prefer `record` types for API contracts (no behavior change yet; guidance only).
9. **Versioned routes** consistently: `/api/v1/...` only (reserve `/internal/*` for ops).
10. **Model binding limits**: set max request body size for public endpoints in docs/runbook.

## B. Performance & allocation discipline
11. **Prefer `ReadOnlySpan<char>`** for hot-path parsing (e.g., currency codes) when implemented.
12. **Avoid LINQ in hot paths**; pre-allocate lists with known capacity.
13. **Use `LoggerMessage.Define`** for high-volume logs to avoid boxing/allocations.
14. **Prefer `ValueTask`** for truly hot async paths where a sync result is likely.
15. **Cache `Regex` with `[GeneratedRegex]`** for stable patterns (ISO-4217, GUIDs).
16. **Use pooled arrays** with `ArrayPool<T>` for transient big buffers (docs-first now).
17. **Avoid `ToLower()`/`ToUpper()`** without culture; use `Equals(..., OrdinalIgnoreCase)`.
18. **Pre-size dictionaries** using expected counts to reduce rehashing.
19. **Seal classes** that are not designed for inheritance (perf + clarity).
20. **Mark structs `readonly`** where all fields are immutable; avoid defensive copies.

## C. Reliability & resiliency
21. **Retry taxonomy** table for providers with backoff defaults.
22. **Circuit breaker** policy documented (minimal Polly pattern; wire later).
23. **Graceful shutdown budget**: 5s default; ensure background queues complete or flush.
24. **Admission control** for expensive endpoints (429 + `Retry-After`).
25. **Time budget** per request: log if over 250ms (warn) / 1s (error) on Null providers.
26. **Ready vs live**: never check external deps on `/health/live`.
27. **Outbox persistence rules** written (even if not yet implemented).
28. **Clock abstraction** (`IClock`) mandated in Core; wall-clock only at edges.
29. **Idempotency store cold-start**: purge expired keys at first use.
30. **Correlation ID echo** always; never generate multiple IDs per request.

## D. Security & privacy
31. **PII redaction helper**: one place to scrub logs; usage documented at boundaries.
32. **Input canonicalization**: trim and reject leading/trailing spaces in currency codes.
33. **Unicode confusables guard** for currency and client IDs (basic homoglyph check).
34. **Deny user-provided URLs** unless on allowlist; use HEAD; no redirects.
35. **Static analysis**: CodeQL workflow active; SLA to triage within 2d.
36. **Gitleaks tuning**: ensure test fixtures allowlisted; keep rule active on PRs.
37. **Dependency audit cadence** documented (Renovate/weekly).
38. **Principle of least privilege for env vars**: known allowlist per service.
39. **CSP for any future dashboard**: documented baseline (no inline scripts).
40. **Webhook signature scheme** documented (HMAC SHA‑256 + timestamp window).

## E. Observability & ops
41. **OpenTelemetry wiring plan** (traces, metrics, logs) documented with namespace `richmove.smartpay.*`.
42. **Pre-allocated log scopes** for frequent fields (corrId, clientId).
43. **Health annotations**: add reason codes in `/ready` (e.g., `config.invalid`, `keystore.unreachable`).
44. **Synthetic monitor scripts**: cURL snippets for quote happy path.
45. **SLOs** posted in repo; error budget burn runbook.
46. **Incident template**: timestamps, corrIds, suspected component, blast-radius.
47. **Rate-limit counters**: standard names + how to read them.
48. **Queue depth metrics** (if/when blockchain enabled): definitions.
49. **Cold-start tracker**: first-request latency logged once per deploy.
50. **Feature-flag change log**: append-only text file in ops docs for toggles.

## F. DX & repo hygiene
51. **Makefile** targets for `analyze`, `contracts`, `perf` (non-failing by default).
52. **.editorconfig (additions)**: enforce parentheses, var preferences, simplify names.
53. **Nullable**: plan to enable project-wide incrementally; start with warnings only.
54. **IDE analyzers**: suggest `in` parameters where beneficial (docs).
55. **Use `IReadOnlyList<>`/`IReadOnlyDictionary<>`** for outward interfaces (guidance).
56. **Public surface review**: ensure internal types are not exposed accidentally.
57. **CODEOWNERS** for Core/API/Docs folders to guarantee review expertise.
58. **PR checklist**: include perf/alloc check when touching hot code.
59. **Commit message convention**: Conventional Commits (docs + template).
60. **Developer onboarding**: one 5‑minute script (Postman + examples + smoke test).

## G. Tests & quality gates
61. **Contract tests** validate all examples (including AUD corridors).
62. **Golden approval files** for rounding/fees (deterministic via `IClock`).
63. **Property-based tests** around currency normalization and amount ranges.
64. **Test data builders** to reduce duplication.
65. **Load smoke** 100 rps/30s (Null providers); p99 < 200ms gate (advisory).
66. **Mutation testing** baseline with realistic thresholds.
67. **Flaky test tracker** doc with quarantine policy.
68. **CI matrix**: Debug + Release build; run tests in Release.
69. **Schema compliance** tests for every public payload (JSON Schema validation).
70. **Postman CI smoke** step (collection runner with two happy paths).

## H. Documentation & contracts
71. **Single source of truth** for error catalog (RFC7807) with stable codes.
72. **OpenAPI examples**: include AUD→EUR and AUD→AED corridors.
73. **SDK snippets** (TS/C#) show idempotency header usage.
74. **Partner checklist** for KYC/FX providers (scopes, rate limits, webhooks).
75. **Feature flags** documented with defaults and blast radius.
76. **ADR index** page listing current decisions.
77. **Currency roster** doc lists Tier‑1 and how to add Tier‑2.
78. **Security posture** page (WAF, rate limits, allowlists).
79. **Runbooks**: toggle flags, rollback, incident response.
80. **Glossary** of terms (idempotency, correlation, outbox, ledger, SLO).

## I. Low-risk code helpers to adopt gradually
81. **`Ensure` guard helpers** with `[MethodImpl(AggressiveInlining)]` for null/empty checks.
82. **`Patterns` static class** with `[GeneratedRegex]` for ISO codes.
83. **`Log` static partial** using `LoggerMessage.Define` for hot logs.
84. **`Ascii` comparers** for token/header comparisons.
85. **`Money` rounding utilities** (docs + helpers, not a domain type).
86. **`Headers` constants** centralizing header names.
87. **`ProblemFactory`** to emit consistent RFC7807 payloads in custom handlers.
88. **`Clock` implementations**: `SystemClock` + `FixedClock` for tests.
89. **`IdGenerator`** based on `Xoshiro`/`Guid` (docs-first; default Guid).
90. **`Utf8Json` helpers** for writer/reader usage (docs + examples).

## J. Security & compliance quick wins
91. **Secrets inventory** doc (owners + rotation cadence).
92. **Key rotation** runbook with commands/placeholders.
93. **DSAR/erasure** process doc (if/when PII stored).
94. **Egress allowlist** doc for provider IPs.
95. **Transport security** checklist (TLS, HSTS defaults) for future dashboards.
96. **Rate-limit tiers** documented (guest, partner, internal).
97. **Auth scopes sketch** for future auth (app vs partner).
98. **Webhook replay window** documented (5 minutes default).
99. **Error message hygiene**: no secrets / internals in ProblemDetails `detail`.
100. **Third-party dependency policy**: criteria to add/remove libraries.

---
### How to land safely
- This patch adds **helpers, analyzers config (as samples), docs, and CI**. No existing classes are rewritten.
- Adopt helpers opportunistically as files are touched. Use the PR checklist to nudge better patterns.