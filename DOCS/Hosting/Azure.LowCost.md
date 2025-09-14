# Azure Hosting — Low‑Cost Patterns for Blazor SSR + API

This guide prioritizes **minimal monthly spend** while keeping SSR fast and realtime reliable.

## Option A — Azure Container Apps (ACA) *scale‑to‑zero*
**When:** Admin console with spiky traffic; OK with cold‑start.
**How:**
- One container image hosting **API + Blazor SSR**; external ingress enabled.
- Attach **Azure SignalR Service** in **serverless** mode for reliable fan‑out.
- Configure **min replicas = 0**, **max replicas = 2**; scale on CPU > 70% (5 min).
- Add a **weekday business‑hours ping** (optional) to reduce cold‑starts.

**Pros:** Lowest idle cost; simple single image.
**Cons:** Cold‑start latency on first request after idle.

## Option B — Azure App Service (Linux) *single small instance*
**When:** You want "always‑on" simplicity without YAML.
**How:**
- Single Web App hosts **API + Blazor SSR**.
- Add **Azure SignalR Service (Classic/Default)** for persistent connections.
- Turn on **Always On** (keeps the app warm), **Compression**, **ARR Affinity** (sticky).

**Pros:** Dead simple; good diagnostics; zero container plumbing.
**Cons:** Cannot scale to 0; a small fixed monthly cost.

## Option C — Split stack for near‑free marketing
- **Static Web Apps** (or GitHub Pages) for docs/marketing (zero‑to‑low cost).
- **ACA/App Service** for the admin app only.

---

## Cost Levers
1. **One instance** only until traffic demands more.
2. **Compression** for responses; **ETags** for GETs.
3. **Output caching** for hot endpoints (30–60s can slash CPU).
4. Scale **SignalR** to minimum unit; connect only on realtime pages.
5. Keep **GC pressure low**: avoid large allocations; stream results where possible.

---

## Operational Tips
- Configure **health endpoints** (`/health/live`, `/health/ready`); tie ACA/App Service probes.
- Log budget breaches (p95 > 300 ms TTFB) as **warnings**; alert on p99 > 1 s sustained.
- Store **OpenAPI** at `/openapi.json` and snapshot in CI for SDK generation.