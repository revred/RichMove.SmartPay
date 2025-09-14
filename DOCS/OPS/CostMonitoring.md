# Cost Monitoring & Alerts

Pragmatic guardrails to catch cost creep early, tuned for **RED** (dev/free) and **GREEN** (prod/paid).

## 1) Budgets (Resource Group)
Create a budget with alerts at **50%**, **80%**, **100%** of the monthly cap.

**Azure CLI (sample):**
```bash
az consumption budget create \
  --amount 50 \  # monthly GBP cap (example)
  --category cost \
  --name rg-budget \
  --resource-group <RG_NAME> \
  --time-grain monthly \
  --thresholds 50 80 100 \
  --contact-emails ops@richmove.co.uk product@richmove.co.uk
```

## 2) App Service / Container Apps alerts
Create rules for:
- **Egress data/day** > 2 GB (RED), 5 GB (GREEN warn), 10 GB (GREEN critical).
- **Avg CPU (5m)** > 70% for 15 minutes (scale/investigate).
- **Requests >= 5xx** > 1% for 5 minutes (reliability guardrail).
- **SignalR connections** > planned cap (e.g., 500).

**Azure CLI (indicative):**
```bash
# Example: CPU > 70% for 15m
az monitor metrics alert create -n cpu-high --scopes <APP_RESOURCE_ID> \
  --condition "avg Percentage CPU > 70" --window-size 15m --evaluation-frequency 5m \
  --action-group <ACTION_GROUP_ID>

# Example: Egress data alert
az monitor metrics alert create -n egress-high --scopes <APP_RESOURCE_ID> \
  --condition "total Bytes Sent > 2147483648" --window-size 1d --evaluation-frequency 1h \
  --action-group <ACTION_GROUP_ID>

# Example: SignalR connections
az monitor metrics alert create -n signalr-connections-high --scopes <SIGNALR_RESOURCE_ID> \
  --condition "avg Connections > 500" --window-size 5m --evaluation-frequency 1m \
  --action-group <ACTION_GROUP_ID>
```

## 3) Logging volume
- Target **< 1 GB/day** in RED; review sampling if exceeded.
- Consider turning off noisy categories or lowering verbosity.

**Log Analytics query to monitor volume:**
```kusto
union withsource=SourceTable *
| where TimeGenerated >= ago(1d)
| summarize LogSizeBytes = sum(estimate_data_size(pack_all()))
| extend LogSizeGB = LogSizeBytes / (1024*1024*1024)
| project LogSizeGB
```

## 4) Prometheus scraping (if enabled)
- In RED: **do not** run persistent Prometheus; rely on local scrape or DevTools.
- In GREEN: scrape **/metrics** at **60s**; no remote write unless justified.

## 5) Monthly Review
- Compare budget vs actuals; track deltas for any enabled WP8 features.
- If any alert breached → **consider disabling** allowlisted features until verified.

## Environment-Specific Thresholds

### RED Environment (Dev/Free Tier)
| Metric | Threshold | Action |
|--------|-----------|--------|
| Monthly Budget | £50 | 50%/80%/100% alerts |
| Egress Data/Day | 2 GB | Warning → investigate |
| Log Volume/Day | 1 GB | Review sampling settings |
| CPU Average (15m) | 70% | Scale or investigate |
| SignalR Connections | 500 | Investigate connection leaks |

### GREEN Environment (Prod/Paid)
| Metric | Warning | Critical | Action |
|--------|---------|----------|--------|
| Monthly Budget | £500 | £750 | Business review required |
| Egress Data/Day | 5 GB | 10 GB | Investigate/throttle |
| Log Volume/Day | 5 GB | 10 GB | Sampling review |
| CPU Average (15m) | 70% | 85% | Auto-scale or investigate |
| SignalR Connections | 2000 | 5000 | Scale SignalR service |

## Cost Attribution for WP8 Features

### Metrics Endpoint (`/metrics`)
- **Expected cost**: Near-zero in RED (no persistent scraping)
- **Cost drivers**: CPU for metric collection, egress if scraped externally
- **Guardrails**: Private binding, admin auth, 60s scrape interval max

### Scaling Status Endpoint (`/scaling/status`)
- **Expected cost**: Zero if rarely accessed
- **Cost drivers**: Minimal CPU for status calculation
- **Guardrails**: Admin auth, no PII, rate limited

### OpenTelemetry
- **Expected cost**: Near-zero with exporters disabled
- **Cost drivers**: Memory for trace collection, egress if exported
- **Guardrails**: Exporters disabled by default, low sampling rate

## Alerting Setup Script

```bash
#!/bin/bash
# setup-cost-alerts.sh

RG_NAME=${1:-"rg-smartpay-red"}
APP_NAME=${2:-"app-smartpay"}
BUDGET_AMOUNT=${3:-50}
CONTACT_EMAIL=${4:-"ops@richmove.co.uk"}

echo "Setting up cost monitoring for $RG_NAME..."

# Create action group
az monitor action-group create \
  --name ag-cost-alerts \
  --resource-group $RG_NAME \
  --short-name CostAlert \
  --email-receivers name=ops email=$CONTACT_EMAIL

ACTION_GROUP_ID=$(az monitor action-group show \
  --name ag-cost-alerts \
  --resource-group $RG_NAME \
  --query id -o tsv)

# Create budget
az consumption budget create \
  --amount $BUDGET_AMOUNT \
  --category cost \
  --name budget-monthly \
  --resource-group $RG_NAME \
  --time-grain monthly \
  --thresholds 50 80 100 \
  --contact-emails $CONTACT_EMAIL

# Get App Service resource ID
APP_RESOURCE_ID=$(az webapp show \
  --name $APP_NAME \
  --resource-group $RG_NAME \
  --query id -o tsv)

# CPU alert
az monitor metrics alert create \
  --name alert-cpu-high \
  --resource-group $RG_NAME \
  --scopes $APP_RESOURCE_ID \
  --condition "avg Percentage CPU > 70" \
  --window-size 15m \
  --evaluation-frequency 5m \
  --action-groups $ACTION_GROUP_ID \
  --description "CPU usage > 70% for 15 minutes"

echo "Cost monitoring setup complete for $RG_NAME"
```

## Dashboard Queries

### Cost Trend Analysis
```kusto
AzureActivity
| where TimeGenerated >= ago(30d)
| where ResourceGroup contains "smartpay"
| summarize Operations = count() by bin(TimeGenerated, 1d), ResourceGroup
| render timechart
```

### Resource Utilization
```kusto
Perf
| where TimeGenerated >= ago(1d)
| where ObjectName == "Processor" and CounterName == "% Processor Time"
| summarize AvgCPU = avg(CounterValue) by bin(TimeGenerated, 1h)
| render timechart
```

## Response Procedures

### Budget Alert Response (50% threshold)
1. Review current month spend breakdown
2. Identify top cost drivers
3. Check if WP8 features are enabled unnecessarily
4. Document findings in monthly cost review

### Budget Alert Response (80% threshold)
1. Immediate investigation required
2. Disable non-essential WP8 features if enabled
3. Review and optimize resource sizing
4. Consider scaling down in RED environment
5. Notify product team of potential overage

### Budget Alert Response (100% threshold)
1. **Emergency response**: Disable all optional features
2. Scale down to minimum viable resources
3. Emergency team meeting within 4 hours
4. Implement immediate cost reduction measures
5. Daily cost monitoring until resolved

## Monthly Cost Review Checklist
- [ ] Actual vs budget comparison
- [ ] Top 5 cost drivers identified
- [ ] WP8 feature cost attribution calculated
- [ ] Trend analysis (month-over-month)
- [ ] Optimization opportunities documented
- [ ] Next month's budget forecast
- [ ] Alert threshold adjustments if needed

## Integration with Feature Flags
Cost monitoring should trigger feature flag reviews:
- Budget breach → Review enabled WP8 features
- High egress → Check if metrics are being scraped externally
- High CPU → Verify if observability overhead is justified
- Alert fatigue → Adjust thresholds or disable noisy features