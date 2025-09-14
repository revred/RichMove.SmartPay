# WP4 Tests

- `NotificationsHubSmokeTests.cs` checks that the negotiate endpoint exists.
- `TenantResolverTests.cs` validates the Host/Header strategies.

> Note: If your solution uses a different test host pattern, move these into your existing test project and ensure references to the API project are present. A minimal `TestAppFactory` (WebApplicationFactory) is expected.

## k6 smoke

```bash
BASE_URL=http://localhost:5001 TENANT=default k6 run ANALYSIS/PERF/scenarios/fx-quote-smoke.js
```

For a quick hit:
```bash
node ANALYSIS/PERF/scenarios/fx-quote-smoke.js # if you wrap with k6 docker or alias
```