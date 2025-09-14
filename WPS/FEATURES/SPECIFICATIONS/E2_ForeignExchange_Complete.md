# Epic E2: Foreign Exchange Core - Complete Specification

## Executive Summary
The Foreign Exchange Core epic provides real-time currency conversion capabilities with institutional-grade pricing, quote persistence, and regulatory compliance for cross-border payment processing.

## Business Context

### Problem Statement
Cross-border payments require accurate, real-time FX rates with transparent pricing, audit trails, and compliance with international financial regulations. Current solutions lack transparency and have high fees.

### Target Users
- **Primary**: E-commerce merchants processing international payments
- **Secondary**: Financial institutions requiring FX services
- **Tertiary**: Individual users sending remittances

### Success Metrics
- Quote-to-execution ratio >85%
- Rate accuracy within 0.1% of mid-market
- Quote generation <200ms p99
- Zero FX-related compliance violations
- Customer satisfaction score >4.5/5

### Business Value
- Revenue: 25-50 bps on FX volume
- Cost reduction: 40% vs traditional providers
- Market differentiation: Real-time transparent pricing
- Compliance: Automated regulatory reporting

## Technical Context

### System Architecture Impact
- Core domain service with high availability requirements
- Real-time rate feed integration
- Event-driven quote lifecycle
- Distributed caching for rate optimization

### Technology Stack
- .NET 9 with FastEndpoints
- PostgreSQL for quote persistence
- Redis for rate caching
- gRPC for rate provider integration
- SignalR for real-time updates

### Integration Points
- External rate providers (Reuters, Bloomberg)
- Payment processors for execution
- Compliance systems for reporting
- Notification service for alerts

### Data Model Changes
- Quote aggregate with full audit trail
- Rate history time-series data
- Currency pair configuration
- Margin rules engine

## Features

### Feature E2.F1: Create FX Quote

#### Overview
Generate binding FX quotes with transparent pricing, configurable margins, and guaranteed rates for specified validity periods.

#### Capabilities

##### Capability E2.F1.C1: Quote Calculation Engine

###### Functional Specification
- **Purpose**: Calculate accurate FX quotes with margins and fees
- **Trigger**: POST /api/fx/quote request
- **Preconditions**:
  - Valid currency pair configured
  - Rate data available (<5 seconds old)
  - Customer authenticated and authorized
  - Compliance checks passed

- **Process**:
  1. Validate input parameters
  2. Check currency pair eligibility
  3. Retrieve base rate from cache/provider
  4. Apply customer-specific margins
  5. Calculate fees based on amount tiers
  6. Apply regulatory requirements
  7. Generate unique quote ID
  8. Calculate expiry timestamp
  9. Persist quote with audit data
  10. Publish quote created event
  11. Return formatted quote response

- **Postconditions**:
  - Quote persisted in database
  - Quote cached for fast retrieval
  - Events published to subscribers
  - Audit trail created

- **Outputs**:
  - Quote object with all details
  - Correlation ID for tracking
  - Rate transparency breakdown

###### Requirements

**Requirement E2.F1.C1.R1**: Input Validation
- **Description**: Comprehensive validation of all quote request parameters
- **Rationale**: Prevent invalid quotes and potential financial loss
- **Priority**: Must Have
- **Acceptance Criteria**:
  - [ ] A1: Validates currency codes against ISO 4217
  - [ ] A2: Ensures amount is positive and within limits (0.01 - 10,000,000)
  - [ ] A3: Checks decimal precision (max 4 decimal places)
  - [ ] A4: Validates quote type (spot, forward, swap)
  - [ ] A5: Ensures value date is valid business day
  - [ ] A6: Checks customer eligibility for currency pair
  - [ ] A7: Validates against sanctions lists
  - [ ] A8: Ensures rate staleness <5 seconds

- **Test Scenarios**:
  - T1: Submit quote with invalid currency code → 400 with specific error
  - T2: Submit negative amount → 400 with validation message
  - T3: Submit amount exceeding limits → 400 with limit details
  - T4: Submit sanctioned currency → 403 with compliance message
  - T5: Submit when rates unavailable → 503 with retry-after header
  - T6: Submit with excessive decimal places → 400 with precision error
  - T7: Submit for restricted currency pair → 403 with eligibility error
  - T8: Submit with stale rates → 503 with rate unavailability error

**Requirement E2.F1.C1.R2**: Rate Calculation Accuracy
- **Description**: Ensure rate calculations maintain financial accuracy
- **Rationale**: Financial calculations must be precise to prevent losses
- **Priority**: Must Have
- **Acceptance Criteria**:
  - [ ] A1: Use decimal arithmetic (no floating point)
  - [ ] A2: Maintain 8 decimal places internally
  - [ ] A3: Round using banker's rounding
  - [ ] A4: Apply margins as basis points precisely
  - [ ] A5: Calculate cross-rates through USD correctly
  - [ ] A6: Handle reciprocal rates accurately
  - [ ] A7: Apply tiered pricing correctly
  - [ ] A8: Include all fees in total calculation

- **Test Scenarios**:
  - T1: Calculate USD/EUR with 25bps margin → Verify exact calculation
  - T2: Calculate cross-rate GBP/JPY → Verify via USD calculation
  - T3: Calculate with tiered margins → Verify tier boundaries
  - T4: Calculate reciprocal rate → Verify mathematical accuracy
  - T5: Edge case: Very small amounts → Verify minimum fee application
  - T6: Edge case: Very large amounts → Verify volume discounts
  - T7: Rounding scenario → Verify banker's rounding applied
  - T8: Multi-fee scenario → Verify fee aggregation

**Requirement E2.F1.C1.R3**: Quote Persistence
- **Description**: Reliably persist quotes with full audit trail
- **Rationale**: Regulatory requirement and dispute resolution
- **Priority**: Must Have
- **Acceptance Criteria**:
  - [ ] A1: Atomic quote storage (all or nothing)
  - [ ] A2: Immutable quote records
  - [ ] A3: Encrypted sensitive data at rest
  - [ ] A4: Indexed for fast retrieval
  - [ ] A5: Partition by date for performance
  - [ ] A6: Include full request context
  - [ ] A7: Store rate snapshot used
  - [ ] A8: Maintain 7-year retention

- **Test Scenarios**:
  - T1: Persist quote during database failover → Verify no data loss
  - T2: Concurrent quote persistence → Verify ACID compliance
  - T3: Query quote by ID → Verify <50ms retrieval
  - T4: Query quotes by date range → Verify index usage
  - T5: Attempt quote modification → Verify immutability
  - T6: Verify encryption → Check data at rest
  - T7: Test retention policy → Verify 7-year data availability
  - T8: Database full scenario → Verify graceful degradation

###### Non-Functional Requirements

**Performance**:
- Response Time: p50 <100ms, p95 <150ms, p99 <200ms
- Throughput: 1000 quotes/second sustained
- Concurrency: 100 simultaneous quote requests
- Rate limiting: 100 quotes/minute per customer

**Reliability**:
- Availability: 99.95% (4.38 hours downtime/year)
- Error Budget: <0.01% quote failures
- Recovery Time: <30 seconds for failover
- Data Durability: 99.999999999% (11 nines)

**Security**:
- Authentication: OAuth2/JWT required
- Authorization: Role-based (Merchant, Admin)
- Encryption: TLS 1.3 in transit, AES-256 at rest
- Rate Limiting: Per customer and global
- Audit: Every quote attempt logged

**Scalability**:
- Horizontal scaling to 10 nodes
- Database sharding by customer ID
- Cache scaling independent of compute
- Event streaming to 1M subscribers

###### Edge Cases

1. **Rapid rate fluctuation**:
   - Detection: Rate change >1% in <1 second
   - Handling: Suspend quoting, alert operators, use last stable rate

2. **Currency pair suspension**:
   - Detection: Central bank announcement
   - Handling: Immediate quote suspension, notify customers, void pending quotes

3. **Leap second handling**:
   - Detection: During leap second window
   - Handling: Pause quoting for 2 seconds, buffer requests

4. **Decimal precision overflow**:
   - Detection: Calculation exceeds 28 digits
   - Handling: Use scientific notation internally, display with fixed precision

5. **Customer limit breach**:
   - Detection: Cumulative volume exceeds limit
   - Handling: Soft block with override capability

6. **Rate provider outage**:
   - Detection: No rate update for 5 seconds
   - Handling: Failover to secondary, then tertiary, then suspend

7. **Timezone cutoff**:
   - Detection: Quote requested at day boundary
   - Handling: Use T+2 settlement rules, adjust value date

8. **Holiday calendar conflicts**:
   - Detection: Different holidays in currency jurisdictions
   - Handling: Use most restrictive calendar, extend settlement

###### Error Scenarios

1. **Database Connection Lost**:
   - Detection: Connection timeout
   - Recovery: Circuit breaker, retry with backoff, failover to replica
   - Customer Impact: 503 with retry-after header
   - Monitoring: Alert on first failure

2. **Rate Feed Corruption**:
   - Detection: Rate fails sanity checks
   - Recovery: Mark feed unhealthy, use alternate source
   - Customer Impact: Potential brief quoting suspension
   - Monitoring: Data quality alerts

3. **Memory Pressure**:
   - Detection: >90% heap usage
   - Recovery: Trigger GC, shed load, scale horizontally
   - Customer Impact: Increased latency
   - Monitoring: Memory alerts at 80%, 90%

4. **Distributed Lock Timeout**:
   - Detection: Lock acquisition timeout
   - Recovery: Exponential backoff, eventual consistency
   - Customer Impact: Duplicate quote possibility
   - Monitoring: Lock contention metrics

5. **Event Publishing Failure**:
   - Detection: Message broker unavailable
   - Recovery: Local queue, replay on recovery
   - Customer Impact: Delayed notifications
   - Monitoring: Queue depth alerts

###### Dependencies
- **Internal**:
  - Rate Service (critical)
  - Customer Service (required)
  - Compliance Service (required)
  - Notification Service (optional)

- **External**:
  - Rate Providers (Reuters primary, Bloomberg backup)
  - Currency Holiday API
  - Sanctions List API
  - Payment Networks (for execution)

###### Monitoring & Observability
- **Metrics**:
  - Quote volume by currency pair
  - Quote-to-execution conversion rate
  - Rate spread analysis
  - Margin capture rate
  - Quote latency histogram
  - Error rate by type

- **Logs**:
  - Every quote request (structured)
  - Rate updates (debug level)
  - Calculation steps (trace level)
  - Errors with full context

- **Alerts**:
  - Quote error rate >0.1%
  - Latency p99 >300ms
  - Rate staleness >10 seconds
  - Margin below threshold
  - Unusual quote patterns (fraud)

##### Capability E2.F1.C2: Rate Management System

[Continue with similar depth for each capability...]

### Feature E2.F2: Persistent Quote Storage

[Elaborate with same level of detail...]

### Feature E2.F3: Dynamic Pricing Engine

[Elaborate with same level of detail...]

### Feature E2.F4: Database Health Monitoring

[Elaborate with same level of detail...]

### Feature E2.F5: Fallback Mode Operation

[Elaborate with same level of detail...]

## Cross-Cutting Concerns

### Security Considerations
- PCI-DSS compliance for payment data
- GDPR compliance for EU customers
- SOX compliance for financial reporting
- Data residency requirements by jurisdiction

### Performance Implications
- Rate caching strategy critical for latency
- Database partitioning for scale
- Event streaming architecture for real-time
- Connection pooling optimization

### Data Consistency Requirements
- Quote immutability after creation
- Event sourcing for audit trail
- Distributed transaction boundaries
- Eventual consistency for notifications

### Transaction Boundaries
- Quote creation is atomic
- Rate updates are eventually consistent
- Payment execution requires 2PC
- Rollback procedures defined

## Migration & Rollout

### Data Migration Requirements
- Historical quotes from legacy system
- Currency configuration import
- Customer preference migration
- Rate history backfill

### Feature Flags Needed
- `fx.quote.enabled` - Master switch
- `fx.quote.margins.dynamic` - Dynamic pricing
- `fx.quote.compliance.strict` - Enhanced checks
- `fx.quote.events.enabled` - Event publishing

### Rollback Procedures
1. Disable feature flags
2. Route to legacy system
3. Replay failed quotes
4. Reconcile quote IDs
5. Notify affected customers

### Training Requirements
- Operations team: Monitoring and alerts
- Support team: Quote investigation
- Sales team: Pricing model
- Compliance team: Reporting tools

## Open Questions
1. Should we support cryptocurrency pairs initially?
2. How to handle negative interest rate scenarios?
3. What is the business continuity plan for rate provider outage?
4. Should we offer rate guarantees beyond standard validity?
5. How to price exotic currency pairs with low liquidity?

## Appendices

### A: Glossary of Terms
- **Mid-market rate**: Average of bid and ask prices
- **Basis point (bp)**: 0.01%
- **Value date**: Settlement date for currency exchange
- **Cross-rate**: Exchange rate calculated via third currency
- **Spot rate**: Exchange rate for immediate delivery

### B: Related Documentation
- FX Industry Standards (ISO 20022)
- Payment Processing Architecture
- Compliance Framework
- Rate Provider Integration Specs

### C: Decision Log
- 2024-01-15: Chose decimal type over double for accuracy
- 2024-02-01: Decided on 5-minute quote validity
- 2024-02-15: Selected banker's rounding over truncation
- 2024-03-01: Approved tiered margin structure