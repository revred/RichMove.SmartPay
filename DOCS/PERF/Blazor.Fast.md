# Blazor Server — Lightning Fast (WP6.2)

- Use `ServerPrerendered`; keep component state tiny.
- Reuse `HttpClient` via `IHttpClientFactory`.
- Prefer plain forms & tables; no heavy UI libs.
- Turn off tracing/metrics in RED; enable sampling only in GREEN.

## Perf budget
- TTFB < 300ms local, < 600ms min-tier GREEN.
- FCP < 1.2s on low-end devices.

## Circuit tips
- Avoid large graphs in `@code` state.
- Stream results if payloads are large (rare in admin).

## Previous guidance (keep for reference)

### Rendering
- Prefer **SSR**; only hydrate small components (`InteractiveOnDemand`).
- Stream partials when lists are slow; never block head/hero.
- Avoid big component libraries; plain Razor + minimal CSS.

### Data
- **Cursor pagination**; page size 25 by default.
- **Output cache** stable GETs for 30–60 s; **ETag** for details.
- Compress with **Brotli**/**Gzip**.

### Realtime
- Connect SignalR **only on pages that need it**; disconnect on navigation.
- Batch UI updates where possible (coalesce events).

### Memory
- Avoid large in‑memory lists in components; use `Virtualize`.
- Dispose subscriptions on `IDisposable`.

### CI Budgets
- TTFB p95 < 300 ms; FCP p95 < 1.2 s.
- Regressions fail the build; publish flame‑graphs for slow paths.

## Patterns

### Island hydration trigger
Hydrate on visibility or interaction; do not auto‑hydrate offscreen tables.

### Toasts for realtime
Use small, ephemeral notifications; avoid re‑rendering entire pages on events.

### "Fast Empty" page shell
Render chromes (nav, header) instantly; lazy load content region.

## Diagnostics
- `dotnet-counters monitor --counters System.Runtime` during smoke.
- Response timing middleware (already in WP4 Analytics) to record p95/p99.
- Capture **server timing** headers for browser DevTools.

## Rollback strategy
- Feature flags allow disabling heavy components or realtime on the fly.
- Fallback to plain SSR (no hubs) if SignalR unit is constrained.