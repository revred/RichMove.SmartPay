# Blazor UI Plan — Lightning‑Fast SSR, Minimal JS

**Key idea:** Render HTML on the server (SSR) for instant first‑paint; hydrate *only* small islands on interaction. Use SignalR for real‑time updates from WP4. No heavy charts/images — just fields and tables.

## Render Modes
- **Server (SSR)** — default for pages; keeps memory use modest if circuits are short‑lived.
- **InteractiveOnDemand** — hydrate a component *after* first paint, on user action or when in viewport.
- **InteractiveAuto** — for tiny widgets (toast, status badge) where auto is fine.

> **Rule:** No page‑wide interactivity unless absolutely required.

## Page Composition
- **Layout**: header (tenant switch), left nav, content area.
- **List pages**: search, filters, cursor pagination, `Virtualize` for long lists.
- **Detail pages**: read‑first (SSR) + discrete edit modals.
- **Create**: dialog with optimistic UI; on success, emit to list via SignalR and toast.

## Real‑time (from WP4)
- Tenant joins group `tenant::<id>` on hub connect.
- Topics include: `fx.quote.created`, `fx.quote.updated`, `kyc.status.changed`.
- UI pattern: patch list in place; do not refetch entire page.

## Performance Budgets & Checks
- **TTFB** < 300 ms (p95) server‑side.
- **First Contentful Paint** < 1.2 s on mid‑range hardware.
- **HTML** < 40 KB per initial view.
- **JS** < 50 KB gzip per page (ideally ~20 KB).
- **Hydration** time < 100 ms for an island.

### Measuring
- **k6**: endpoint latency (p50/p95/p99).
- **Playwright**: TTFB + FCP budgets in CI; fail build on regression.
- **dotnet-counters**: CPU, GC, threadpool saturation during smoke.

## Data Access Strategy
1. SSR **loads first fold** on the server handler.
2. Infinite scroll uses **cursor pagination** (`?cursor=&limit=`).
3. API responses are **field‑only** (no HTML); UI composes view‑models.
4. **Idempotency‑Key** on POST/PUT to avoid duplicates on retry.

## Caching
- **Output caching** for stable GETs (e.g., currency lists) 5–15 min.
- **ETag/If‑None‑Match** for detail GETs.
- **Client caching** for static assets with file hashes.

## Error Handling
- Use RFC7807 `ProblemDetails` from API.
- Show non‑blocking banner; retry options for idempotent operations.

## Tenancy & Theming
- Inject `TenantContext` into layout; theme per tenant (light CSS vars only).
- Guard routes by RBAC; hide controls not permitted by role.

## Example User Flows (pseudocode only)

### Quotes: list + create
```razor
@* SSR renders the list; creation is an interactive island *@
<QuoteList InitialItems="Model.Quotes" RenderMode="InteractiveOnDemand" />
<CreateQuoteDialog RenderMode="InteractiveOnDemand" />
```

```csharp
// In CreateQuoteDialog.razor.cs (pseudocode)
var client = SdkFactory.Create(HttpContext, tenantId);
await client.Fx.CreateQuoteAsync(new FxQuoteRequest { FromCurrency="USD", ToCurrency="GBP", Amount=1000m },
                                 idempotencyKey: Guid.NewGuid().ToString("N"));
// Success → close dialog; SignalR event updates list (fx.quote.created)
```

### Realtime patch
```csharp
// In QuoteList.razor.cs (pseudocode)
hub.On<object>("fx.quote.created", payload => {
    Quotes.Insert(0, Map(payload));
    StateHasChanged();
});
```

## Accessibility (A11y)
- Focus management on dialog open/close.
- Semantic tables with `<thead>/<tbody>`, proper `<th scope="col">`.
- Color‑contrast AA; large hit‑targets on mobile.

## Testing
-- **bUnit**: component logic.
- **Playwright**: auth, nav, create quote, realtime arrival.
- **k6**: smoke for `/api/fx/quote` + negotiate endpoint (already in WP4.1).

## Rollout Plan
1. Skeleton layout + auth.
2. Quotes list (SSR), create dialog (island), realtime patch.
3. Rates & Tenants pages.
4. Performance pass (budgets enforced in CI).

---

### FAQ
**Why not Blazor WASM?** Higher JS payload, more moving parts, and no SSR by default—overkill for forms/tables.
**Will SignalR be expensive?** Use Azure SignalR Service with the smallest unit and only connect on pages that need realtime.
**How to keep costs near zero?** Prefer Azure Container Apps scale‑to‑zero or a single tiny App Service instance; split marketing/docs to static hosting.