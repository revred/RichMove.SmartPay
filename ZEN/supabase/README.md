# Supabase Setup (Staging/Prod)

> **Red-first**: Use a single FREE project now: **smartpay-red**. Create **smartpay-green (PAID)** only when going live.
> See **/SECRETS.md** for where to find secrets and how to set them safely.

1) Install CLI and init:
```bash
brew install supabase/tap/supabase
supabase init
```

2) Link & set remote (smartpay-red):
```bash
supabase link --project-ref <staging-ref>
supabase db remote set "postgres://postgres:<PASSWORD>@<HOST>:6543/postgres"
```

3) Push migrations:
```bash
supabase db push
```

4) Enable in API (after linking/pushing) by setting in environment:
```
Environment__Name=red
Supabase__Enabled=true
Supabase__DbConnectionString="Host=<HOST>;Port=6543;Database=postgres;Username=postgres;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true"
```

> Keep Service Role keys only in server-side secrets. Never expose to clients.

5) Later, when going live (create **smartpay-green**):
```bash
supabase link --project-ref <prod-ref>
supabase db remote set "postgres://postgres:<PASSWORD>@<HOST>:6543/postgres"
supabase db push
```