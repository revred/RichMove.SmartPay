# Secrets Setup — smartpay-red (FREE)

This project uses a **red-first** approach: run everything on a single FREE Supabase project (**smartpay-red**) until we go live. Then we'll create **smartpay-green** (PAID) and switch.

> ⚠️ Never commit real secrets. Use the provided sample file and the `secrets/` folder which is git-ignored by default.

## 1) Create your local secrets file
Create this file (do **not** commit it):

```
secrets/SMARTPAY-RED.secrets.env
```

Copy the template from `secrets/SMARTPAY-RED.secrets.sample.env` and fill in the values from Supabase.

## 2) Where to find each secret in Supabase
Open your **smartpay-red** project in the Supabase dashboard.

### A) Project URL, anon key, service role key
- **Left menu → Settings → API**
- Copy:
  - **Project URL** → `Supabase__Url`
  - **anon public** API key → `Supabase__AnonKey` (frontend-only)
  - **service_role** secret → `Supabase__ServiceRoleKey` (server-only; never expose to clients)

### B) Database connection string
- **Left menu → Settings → Database**
- Section: **Connection info** (or **Connection string**)
- Prefer the **pooled** connection (PgBouncer) for app servers:
  - Host: `db.<hash>.supabase.co`
  - Port: **6543** (pooled). Port **5432** is direct Postgres (CLI/admin ok).
- Compose `Supabase__DbConnectionString`, e.g.:
  ```
  Host=db.xxxxx.supabase.co;Port=6543;Database=postgres;Username=postgres;Password=<YOUR_DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true
  ```
- If you don't recall the DB password, set/reset it on the same page.

### C) Project Reference (optional, for CLI)
- **Left menu → Settings → General**
- Field: **Project Reference** (used by `supabase link --project-ref <ref>`)

## 3) Load secrets locally
### Option A — temporary env for a single shell
**macOS/Linux (bash/zsh):**
```bash
set -a
source secrets/SMARTPAY-RED.secrets.env
set +a
dotnet run --project ZEN/SOURCE/Api
```

**Windows (PowerShell):**
```powershell
Get-Content secrets/SMARTPAY-RED.secrets.env | ForEach-Object {
  if ($_ -match '^\s*#') { return }
  $kv = $_.Split('=',2)
  if ($kv.Length -eq 2) { [Environment]::SetEnvironmentVariable($kv[0], $kv[1]) }
}
dotnet run --project ZEN/SOURCE/Api
```

### Option B — .NET user-secrets (dev-only)
```bash
cd ZEN/SOURCE/Api
dotnet user-secrets init
dotnet user-secrets set "Environment:Name" "red"
dotnet user-secrets set "Supabase:Enabled" "true"
dotnet user-secrets set "Supabase:Url" "<Project URL>"
dotnet user-secrets set "Supabase:AnonKey" "<anon key>"
dotnet user-secrets set "Supabase:ServiceRoleKey" "<service role key>"
dotnet user-secrets set "Supabase:DbConnectionString" "Host=...;Port=6543;Database=postgres;Username=postgres;Password=...;SSL Mode=Require;Trust Server Certificate=true"
```

## 4) GitHub Actions — staging env only (for now)
**Repo → Settings → Environments → `staging` → Add secret:**

- `SUPABASE_URL` → Project URL
- `SUPABASE_DB_CONNECTION_STRING` → full Npgsql connection string (use port 6543)
- `SUPABASE_ANON_KEY` → anon key (if needed by build)
- `SUPABASE_SERVICE_ROLE_KEY` → service role (server-only; restrict usage)

Optional: mirror the ASP.NET hierarchical keys if your workflow reads them directly:
- `Environment__Name=red`
- `Supabase__Enabled=true`
- `Supabase__Url`, `Supabase__AnonKey`, `Supabase__ServiceRoleKey`, `Supabase__DbConnectionString`

## 5) Quick verification
Run the API and hit:
```
GET /v1/health/env
```
You should see:
```json
{
  "environment": "red",
  "supabaseEnabled": true,
  "supabaseDbConfigured": true,
  "rateSource": "SupabaseFxRateSource",
  "pricingProvider": "SupabasePricingProvider" or "CombinedFxPricingProvider",
  "quoteRepository": "SupabaseFxQuoteRepository"
}
```

## 6) Safety & rotation
- Never expose `Supabase__ServiceRoleKey` to browsers/mobile clients.
- Rotate keys if leaked: **Settings → API → Regenerate**.
- Prefer **pooled (6543)** in production; use **5432** for admin/CLI.

## 7) Going live later
- Create **smartpay-green** (PAID), push the same migrations, and switch API secrets to point at green.