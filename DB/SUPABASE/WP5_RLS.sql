-- WP5: Multi-tenant Row-Level Security (RLS) templates for Supabase/Postgres
-- Assumptions:
--   * Table "public.quotes" exists with a "tenant_id" text column.
--   * JWT tokens carry a claim "tenant_id" (for anon/user clients).
--   * Service role key (server) bypasses RLS when needed.

-- 1) Ensure tenant_id column exists
ALTER TABLE IF EXISTS public.quotes
  ADD COLUMN IF NOT EXISTS tenant_id text NOT NULL DEFAULT 'default';

-- 2) Enable RLS
ALTER TABLE public.quotes ENABLE ROW LEVEL SECURITY;

-- 3) Policies
DROP POLICY IF EXISTS "tenant_isolation_select" ON public.quotes;
CREATE POLICY "tenant_isolation_select"
  ON public.quotes
  FOR SELECT
  USING (
    tenant_id = coalesce( current_setting('request.jwt.claims', true)::jsonb ->> 'tenant_id', 'default')
  );

DROP POLICY IF EXISTS "tenant_isolation_insert" ON public.quotes;
CREATE POLICY "tenant_isolation_insert"
  ON public.quotes
  FOR INSERT
  WITH CHECK (
    tenant_id = coalesce( current_setting('request.jwt.claims', true)::jsonb ->> 'tenant_id', 'default')
  );

DROP POLICY IF EXISTS "tenant_isolation_update" ON public.quotes;
CREATE POLICY "tenant_isolation_update"
  ON public.quotes
  FOR UPDATE
  USING (
    tenant_id = coalesce( current_setting('request.jwt.claims', true)::jsonb ->> 'tenant_id', 'default')
  )
  WITH CHECK (
    tenant_id = coalesce( current_setting('request.jwt.claims', true)::jsonb ->> 'tenant_id', 'default')
  );

-- 4) Optional: Restrict deletes to service-role only (no policy for anon/user)
DROP POLICY IF EXISTS "tenant_delete" ON public.quotes;

-- Notes:
-- * For Supabase, anon/user keys include JWT; service_role bypasses RLS.
-- * Ensure API layer sets "tenant_id" in tokens or via triggers during inserts.

-- 5) Helpful: index for tenant queries
CREATE INDEX IF NOT EXISTS idx_quotes_tenant ON public.quotes(tenant_id);