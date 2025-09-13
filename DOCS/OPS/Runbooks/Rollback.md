# Runbook — Emergency Rollback

## Overview
Emergency procedures for rolling back problematic deployments.

## Rollback Triggers
Initiate rollback if any of these conditions occur:
- [ ] Error rate > 5% for 5+ minutes
- [ ] P95 latency > 500ms for 5+ minutes
- [ ] Health checks failing
- [ ] Data corruption detected
- [ ] Security incident

## Rollback Process

### 1. Immediate Response
- [ ] **ALERT TEAM**: Notify platform team and stakeholders
- [ ] **DOCUMENT**: Record start time and symptoms
- [ ] **ASSESS**: Determine if rollback is required (vs. hotfix)

### 2. Identify Versions
```bash
# Current running version
kubectl get deployments -o wide
# Or check application logs for version info

# Last known good version
git log --oneline -10
# Look for last successful deployment
```

### 3. Execute Rollback
```bash
# Docker/Kubernetes rollback
kubectl rollout undo deployment/smartpay-api

# Or redeploy specific version
kubectl set image deployment/smartpay-api app=smartpay:v1.2.3
```

### 4. Verification
- [ ] **Health Check**: Verify `/health/ready` returns 200
- [ ] **Smoke Test**: Run critical path test (FX quote)
- [ ] **Monitor**: Watch error rates for 15 minutes
- [ ] **Capacity**: Ensure no performance degradation

### 5. Post-Rollback
- [ ] **Update Status Page**: Inform users of resolution
- [ ] **Root Cause**: Schedule incident review within 24h
- [ ] **Timeline**: Document detailed timeline with correlation IDs
- [ ] **Follow-up**: Plan fix and re-deployment strategy

## Quick Commands

### Check Current Health
```bash
curl -f http://api/health/ready || echo "UNHEALTHY"
```

### View Recent Logs
```bash
kubectl logs -f deployment/smartpay-api --tail=100
```

### Check Error Rate
```bash
# Monitor HTTP 5xx responses
# Use your observability tools (Grafana/DataDog)
```

## Escalation
- **P0 (Data Loss/Security)**: Page CTO immediately
- **P1 (Service Down)**: Page engineering manager
- **P2 (Degraded)**: Standard on-call rotation

## Test Rollback
- [ ] Practice rollback quarterly during low-traffic windows
- [ ] Document actual time to complete rollback
- [ ] Update runbook with lessons learned