# Error Catalog (ProblemDetails)

| Code | `type`                             | `title`               | HTTP | Notes |
|------|------------------------------------|-----------------------|------|------|
| E-IDEMP-001 | about:blank/idempotency-key-missing | Idempotency key required | 400 | Write endpoints require `Idempotency-Key`. |
| E-IDEMP-002 | about:blank/idempotency-conflict    | Duplicate request       | 409 | Same key seen within 24h window. |
| E-TIMEOUT-001 | about:blank/timeout               | Request timed out       | 408 | Cancellation/timeout. |
| E-UNHANDLED-001 | about:blank/unhandled           | Unhandled error         | 500 | Catch-all. |
| E-NOTIMPL-001 | about:blank/not-implemented       | Feature not implemented | 501 | Feature disabled by flag. |

**Conventions**
- Stable `type` URIs, programmatic `code`s (E-xxx), human-readable `title` and `detail`.
- Extend per feature with prefix (`E-FX-*`, `E-KYC-*`). Keep a single source of truth here.