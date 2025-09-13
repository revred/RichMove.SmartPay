# Service Level Objectives (SLOs) & Error Budgets

## Overview
SLOs define the reliability targets for SmartPay API and establish error budgets for planned maintenance and feature development.

## SLO Definitions

| Metric | SLO Target | Measurement Window | Error Budget | Notes |
|--------|------------|-------------------|--------------|-------|
| **Availability** | 99.9% | 30 days | 43.2 minutes | Health + quote path |
| **Latency (P95)** | < 150ms | 30 days | - | For FX quote endpoint |
| **Error Rate** | < 0.1% | 30 days | 0.1% of requests | Excludes 4xx client errors |
| **Cold Start** | < 1.5s | 30 days | - | Container startup time |

## Error Budget Policy

### Budget Exhaustion Actions
- **100% budget used**: Freeze feature development, focus on reliability
- **75% budget used**: Reliability review meeting, slow down releases
- **50% budget used**: Start reliability improvements
- **<25% budget used**: Normal development pace

### Budget Calculation
```
Error Budget = (1 - SLO) × Total Requests
Example: (1 - 0.999) × 1,000,000 = 1,000 error budget
```

## Monitoring & Alerting

### Key Metrics
- **Availability**: `(successful_requests / total_requests) * 100`
- **Latency**: P50, P95, P99 response times
- **Error Rate**: `(5xx_responses / total_requests) * 100`
- **Throughput**: Requests per second

### Alert Thresholds
- **Critical**: Error rate > 0.5% for 5+ minutes
- **Warning**: P95 latency > 300ms for 10+ minutes
- **Info**: Error budget burn > 10x normal rate

## SLO Review Process

### Monthly Review
- [ ] Calculate actual vs target SLOs
- [ ] Review error budget consumption
- [ ] Identify top reliability improvements
- [ ] Plan capacity and performance work

### Quarterly Review
- [ ] Assess SLO targets for reasonableness
- [ ] Update targets based on business requirements
- [ ] Review alert thresholds and noise
- [ ] Update documentation

## Dependencies
Our SLOs depend on:
- **External FX Providers**: May impact latency/availability
- **Database**: Affects all operations
- **Load Balancer/CDN**: Affects availability measurements
- **Monitoring Systems**: Must be available to measure SLOs

## Exceptions
SLO violations may be excused for:
- [ ] Scheduled maintenance (with advance notice)
- [ ] External provider outages (documented)
- [ ] DDoS attacks or security incidents
- [ ] Infrastructure provider issues

## Tools & Dashboards
- **Primary Dashboard**: [Link to Grafana/DataDog]
- **SLO Tracking**: [Link to SLO dashboard]
- **Error Budget**: [Link to burn rate tracking]
- **Alerts**: [Link to PagerDuty/AlertManager]