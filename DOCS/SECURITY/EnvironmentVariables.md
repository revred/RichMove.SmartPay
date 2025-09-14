# Environment Variable Security - Principle of Least Privilege

## Known Allowlist Per Service

### SmartPay API Service

**Required Variables:**
```bash
# Database
SUPABASE_CONNECTION_STRING=    # PostgreSQL connection for blockchain features
CONNECTIONSTRINGS__SUPABASE=   # Alternative format

# Feature Flags
FEATURES__BLOCKCHAINENABLED=   # true/false - enable blockchain endpoints
BLOCKCHAIN__ENABLED=           # Alternative format

# Idempotency Configuration
IDEMPOTENCY__HOURS=           # TTL for idempotency keys (default: 24)

# Logging
ASPNETCORE_ENVIRONMENT=       # Development/Staging/Production
LOGGING__LOGLEVEL__DEFAULT=   # Information/Warning/Error
```

**Prohibited Variables:**
- `ASPNETCORE_URLS` (use appsettings.json)
- `DOTNET_*` system variables (container-managed)
- Any `*_PASSWORD` or `*_SECRET` not explicitly listed
- Development keys in production environments

## Security Validation

### Startup Validation
```csharp
public static void ValidateEnvironmentVariables()
{
    var allowedPrefixes = new[]
    {
        "SUPABASE_",
        "CONNECTIONSTRINGS__",
        "FEATURES__",
        "BLOCKCHAIN__",
        "IDEMPOTENCY__",
        "ASPNETCORE_ENVIRONMENT",
        "LOGGING__"
    };

    var environmentVars = Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .Where(entry => entry.Key.ToString()?.StartsWith("SMARTPAY_") == true ||
                       allowedPrefixes.Any(prefix =>
                           entry.Key.ToString()?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true));

    foreach (DictionaryEntry envVar in environmentVars)
    {
        Log.EnvironmentVariableLoaded(envVar.Key.ToString());
    }
}
```

### Production Checklist

- [ ] No hardcoded secrets in environment variables
- [ ] All production secrets use secure secret management (Azure KeyVault, etc.)
- [ ] Environment variables validated against allowlist at startup
- [ ] Development variables removed from production deployments
- [ ] Logging redacts sensitive environment variable values

### Secret Management Hierarchy

1. **Azure KeyVault** (Production) - Highest security
2. **Container Secrets** (Staging) - Secure runtime injection
3. **Environment Variables** (Development) - Local development only
4. **appsettings.json** (Configuration) - Non-sensitive config only

## Monitoring

Log all environment variable access for security auditing:

```csharp
private static readonly Action<ILogger, string, Exception?> _envVarAccessed =
    LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3001),
        "Environment variable accessed: {VariableName}");
```

## Container Security

```dockerfile
# Remove unnecessary environment variables in production
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

# Only include essential variables
COPY --from=build /app/publish .
USER app
```