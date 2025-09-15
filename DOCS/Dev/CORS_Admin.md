# Dev CORS for Admin ↔ API

## Recommended setup
- **Same-origin** in production (reverse proxy routes `/admin` and `/api` under one domain).
- In **DEV**, avoid hardcoding ports:
  - API reads `Cors:AllowedOrigins` from configuration.
  - If not set, it auto-allows any `localhost:*` origin.
  - Admin reads `ApiBaseUrl` from configuration or falls back to its own origin.

## Configuration
`ZEN/SOURCE/Api/appsettings.Development.json`
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:50985",
      "http://localhost:50985"
    ]
  }
}
```

`ZEN/ADMIN/SmartPay.AdminBlazor/appsettings.Development.json`
```json
{ "ApiBaseUrl": "https://localhost:7169" }
```

> Tip: Prefer setting these via **environment variables** in CI or container orchestration.

## Swagger Fix
The hotfix addresses Swagger 500 errors by using full-type schema IDs to avoid naming collisions:
```csharp
o.CustomSchemaIds = t => t.FullName!.Replace("+", ".");
```

## Testing
1. **Quick Start**: `ZEN/TOOLS/start-services.sh` - Starts both API and Admin services
2. **Stop Services**: `ZEN/TOOLS/kill-services.sh` - Kills all running SmartPay services
3. **Manual Start**:
   - API: `cd ZEN/SOURCE/Api && dotnet run`
   - Admin: `cd ZEN/ADMIN/SmartPay.AdminBlazor && dotnet run`
4. **Verification**:
   - Swagger UI: `http://localhost:5268/swagger`
   - API Health: `http://localhost:5268/health/live`
   - Admin UI: `https://localhost:50985`
   - Verify Admin can call API endpoints without CORS errors