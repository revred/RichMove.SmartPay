# Supabase RLS — Tenant Isolation (WP5)

This guide enables per-tenant isolation on `public.quotes` using **Row-Level Security (RLS)**. It assumes your tokens carry a `tenant_id` claim.

## 1) Apply RLS templates
Run `DB/SUPABASE/WP5_RLS.sql` on your Supabase project (SQL editor or migration). This:
1. Ensures `tenant_id` column exists.
2. Enables RLS.
3. Adds SELECT/INSERT/UPDATE policies that match `tenant_id` from JWT claims.
4. Omits DELETE policy (delete requires service-role key by default).

## 2) JWT claims (tenant_id)
You have options:
- **User-based**: put `tenant_id` into JWT via your auth hook.
- **Server-to-DB**: use service role (bypasses RLS) and set `tenant_id` in the API layer upon insert/update.

> If using service-role for writes from the API, ensure your code assigns `tenant_id` before persisting records.

## 3) Testing
1. With **anon/user key**, query quotes and observe only the current tenant's rows.
2. With **service-role key**, all rows are visible (admin path)—use with care.

## 4) Notes
- Add similar policies to any other multi-tenant tables.
- Index `tenant_id` for performance.
- Consider a view per tenant for analytics if needed.