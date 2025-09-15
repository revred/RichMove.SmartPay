# Supabase RLS — Verification Steps (WP5)

Goal: prove tenant isolation is actually enforced.

## Steps
1. Create tenants A and B; insert sample data for both.
2. Connect with `tenant_id` claim for A and attempt to query B's rows → expect 0.
3. Repeat for inverse.
4. Attempt to insert/update/delete across tenants → expect RLS error.
5. Record evidence (screenshots/SQL outputs) and attach to `DOCS/VnV/Evidence/RLS/*.md`.

## Smoke
Add a smoke script calling the API with a tenant header and verifying only that tenant's entities are returned.