# Runbook — Toggle Feature Flags

## Overview
SmartPay uses feature flags to control optional functionality and enable safe rollouts.

## Available Flags

| Flag | Default | Purpose |
|------|---------|---------|
| `BlockchainEnabled` | `false` | Enable blockchain ledger writes |
| `QuotesCacheEnabled` | `false` | Enable FX quote caching |
| `RateLimitEnabled` | `true` | Enable API rate limiting |

## Configuration
Flags are configured via environment variables or appsettings:

```bash
# Environment variables (preferred)
FEATURES__BlockchainEnabled=true
FEATURES__QuotesCacheEnabled=false
FEATURES__RateLimitEnabled=true

# Or appsettings.json
{
  "Features": {
    "BlockchainEnabled": true,
    "QuotesCacheEnabled": false,
    "RateLimitEnabled": true
  }
}
```

## Safe Toggle Process

### 1. Pre-Toggle Check
- [ ] Verify current flag state via logs or health endpoints
- [ ] Check system load and error rates
- [ ] Ensure monitoring dashboards are available

### 2. Toggle Flag
- [ ] Update configuration (environment/settings)
- [ ] Restart application if required
- [ ] Verify `/health/ready` returns 200 OK

### 3. Post-Toggle Monitoring
- [ ] Monitor logs for 15 minutes after toggle
- [ ] Check error rates and performance metrics
- [ ] Verify expected behavior changes

### 4. Rollback Plan
If issues detected:
- [ ] Immediately revert flag to previous state
- [ ] Document incident with timestamps
- [ ] Include correlation IDs from affected requests
- [ ] File post-incident review

## Emergency Contacts
- Platform Team: [contact info]
- On-Call: [rotation schedule]

## Common Scenarios

### Enable Blockchain
```bash
# Enable blockchain ledger
FEATURES__BlockchainEnabled=true

# Verify ledger writes appear in logs
# Check blockchain endpoints return data instead of 404
```

### Disable Rate Limiting (Emergency)
```bash
# Temporarily disable if false positives
FEATURES__RateLimitEnabled=false

# Monitor for abuse - re-enable ASAP
```