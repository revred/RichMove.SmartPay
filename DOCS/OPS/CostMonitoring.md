# Cost Monitoring (Azure-first)

## Budgets
- Azure Cost Management Budget: set monthly budget with email alert at 50/80/100%.

## App Service / Container Apps
- Keep min instances = 0 or 1; disable always-on in RED.
- Turn off diagnostics where not needed.

## Metrics sampling
- Export only when needed; prefer text `/metrics` scraped privately.