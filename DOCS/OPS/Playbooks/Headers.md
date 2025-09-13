# Standard Headers

| Header             | Direction | Required | Notes |
|--------------------|-----------|----------|-------|
| `X-Correlation-Id` | in/out    | yes      | Echoed on responses; propagated to providers. |
| `Idempotency-Key`  | in        | for writes | Unique per write request for 24h window. |
| `Content-Type`     | in        | yes      | `application/json` for API POST bodies. |
| `Accept`           | in        | optional | Prefer `application/json`. |

**Conventions**
- Header names are case-insensitive; we keep canonical casing above.
- Correlation IDs are GUIDs (36 chars); any opaque string is accepted.