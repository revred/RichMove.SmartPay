# Requirements Extraction Checklist

## Purpose
Systematic checklist to ensure all implicit requirements and unwritten expectations are captured for each feature.

## For Each Feature, Ask:

### 🎯 Functional Requirements

#### Core Functionality
- [ ] What is the primary purpose?
- [ ] What problem does it solve?
- [ ] Who are the users?
- [ ] What triggers the feature?
- [ ] What are the inputs?
- [ ] What are the outputs?
- [ ] What are the side effects?

#### Business Rules
- [ ] What business constraints apply?
- [ ] What calculations are performed?
- [ ] What validations are required?
- [ ] What are the decision points?
- [ ] What are the approval workflows?
- [ ] What are the limits and thresholds?

#### Data Requirements
- [ ] What data is read?
- [ ] What data is created?
- [ ] What data is modified?
- [ ] What data is deleted?
- [ ] What is the data retention policy?
- [ ] What are the data quality requirements?
- [ ] What are the data privacy requirements?

### ⚡ Performance Requirements

#### Response Time
- [ ] What is acceptable latency for users?
- [ ] What is the target response time (p50, p95, p99)?
- [ ] Are there different SLAs for different operations?
- [ ] What operations can be asynchronous?
- [ ] What requires real-time response?

#### Throughput
- [ ] Expected requests per second?
- [ ] Peak load scenarios?
- [ ] Seasonal variations?
- [ ] Growth projections?
- [ ] Batch processing requirements?

#### Resource Utilization
- [ ] CPU usage targets?
- [ ] Memory consumption limits?
- [ ] Network bandwidth requirements?
- [ ] Storage growth rate?
- [ ] Connection pool sizes?

### 🔒 Security Requirements

#### Authentication & Authorization
- [ ] Who can access this feature?
- [ ] What authentication methods are supported?
- [ ] What are the authorization rules?
- [ ] How are permissions inherited?
- [ ] What about delegation?
- [ ] Service account access?

#### Data Protection
- [ ] What data needs encryption at rest?
- [ ] What data needs encryption in transit?
- [ ] What are the key management requirements?
- [ ] PII handling requirements?
- [ ] Data masking needs?
- [ ] Audit trail requirements?

#### Threat Mitigation
- [ ] What are the potential attack vectors?
- [ ] Input validation requirements?
- [ ] Rate limiting needs?
- [ ] DDoS protection?
- [ ] Injection attack prevention?
- [ ] Session management requirements?

### 🔄 Reliability Requirements

#### Availability
- [ ] Required uptime percentage?
- [ ] Maintenance window allowances?
- [ ] Geographic availability?
- [ ] Disaster recovery requirements?
- [ ] Backup frequency and retention?

#### Fault Tolerance
- [ ] What failures must be handled?
- [ ] Retry strategies?
- [ ] Circuit breaker patterns?
- [ ] Fallback mechanisms?
- [ ] Graceful degradation paths?
- [ ] Error recovery procedures?

#### Data Integrity
- [ ] Transaction requirements (ACID)?
- [ ] Consistency guarantees?
- [ ] Idempotency requirements?
- [ ] Duplicate detection?
- [ ] Reconciliation needs?

### 📈 Scalability Requirements

#### Horizontal Scaling
- [ ] Can it scale horizontally?
- [ ] Stateless or stateful?
- [ ] Session affinity requirements?
- [ ] Load balancing strategy?
- [ ] Auto-scaling triggers?

#### Vertical Scaling
- [ ] Resource limits per instance?
- [ ] When to scale up vs out?
- [ ] Cost optimization considerations?

#### Data Scaling
- [ ] Sharding strategy?
- [ ] Partitioning scheme?
- [ ] Archive strategy?
- [ ] Read replica needs?
- [ ] Caching strategy?

### 🔍 Observability Requirements

#### Logging
- [ ] What events must be logged?
- [ ] Log levels for different scenarios?
- [ ] Structured logging requirements?
- [ ] Log retention policies?
- [ ] Sensitive data exclusions?

#### Monitoring
- [ ] Key metrics to track?
- [ ] Custom metrics needed?
- [ ] Dashboard requirements?
- [ ] Real-time monitoring needs?
- [ ] Historical analysis requirements?

#### Alerting
- [ ] Alert conditions?
- [ ] Severity levels?
- [ ] Escalation paths?
- [ ] Alert fatigue prevention?
- [ ] On-call procedures?

#### Tracing
- [ ] Distributed tracing needs?
- [ ] Correlation ID requirements?
- [ ] Request flow tracking?
- [ ] Performance profiling needs?

### 🌍 Internationalization Requirements

#### Localization
- [ ] Languages to support?
- [ ] Right-to-left language support?
- [ ] Date/time format variations?
- [ ] Number format variations?
- [ ] Currency display requirements?

#### Regional Compliance
- [ ] Data residency requirements?
- [ ] Regional regulations (GDPR, CCPA)?
- [ ] Tax calculation variations?
- [ ] Legal entity considerations?

### ♿ Accessibility Requirements

#### Standards Compliance
- [ ] WCAG level (A, AA, AAA)?
- [ ] Screen reader compatibility?
- [ ] Keyboard navigation support?
- [ ] Color contrast requirements?
- [ ] Alternative text requirements?

### 🔧 Operational Requirements

#### Deployment
- [ ] Deployment frequency?
- [ ] Zero-downtime deployment?
- [ ] Rollback capabilities?
- [ ] Feature flag requirements?
- [ ] Environment-specific configs?

#### Maintenance
- [ ] Maintenance window requirements?
- [ ] Data cleanup procedures?
- [ ] Certificate rotation?
- [ ] Dependency updates?
- [ ] Database maintenance?

#### Support
- [ ] Support tier (L1, L2, L3)?
- [ ] Documentation requirements?
- [ ] Troubleshooting guides?
- [ ] Known issues tracking?
- [ ] Customer communication needs?

### 🔗 Integration Requirements

#### External Systems
- [ ] Third-party integrations?
- [ ] API versioning strategy?
- [ ] Backwards compatibility?
- [ ] Deprecation policy?
- [ ] SLA dependencies?

#### Internal Systems
- [ ] Microservice dependencies?
- [ ] Shared libraries?
- [ ] Message queue interactions?
- [ ] Database connections?
- [ ] Cache dependencies?

### 📊 Compliance Requirements

#### Regulatory
- [ ] Financial regulations (PCI-DSS, SOX)?
- [ ] Data protection (GDPR, CCPA)?
- [ ] Healthcare (HIPAA)?
- [ ] Industry-specific (ISO 27001)?

#### Audit
- [ ] Audit log requirements?
- [ ] Compliance reporting?
- [ ] Evidence collection?
- [ ] Retention periods?

### 💰 Cost Requirements

#### Development Cost
- [ ] Effort estimation?
- [ ] Team composition?
- [ ] Timeline constraints?
- [ ] Technical debt considerations?

#### Operational Cost
- [ ] Infrastructure costs?
- [ ] License costs?
- [ ] Support costs?
- [ ] Third-party service costs?

#### Business Impact
- [ ] Revenue impact?
- [ ] Cost savings?
- [ ] Risk mitigation value?
- [ ] Competitive advantage?

## Unwritten Expectations Often Missed

### User Experience
- [ ] "It should just work"
- [ ] Consistency with existing features
- [ ] Intuitive error messages
- [ ] Progressive disclosure of complexity
- [ ] Undo/redo capabilities
- [ ] Bulk operations support
- [ ] Export/import capabilities
- [ ] Mobile responsiveness

### Developer Experience
- [ ] Code should be self-documenting
- [ ] APIs should be RESTful and predictable
- [ ] Errors should include resolution hints
- [ ] Testing should be straightforward
- [ ] Local development should be simple
- [ ] Debugging should be easy

### Business Assumptions
- [ ] Future growth accommodation
- [ ] Multi-tenant readiness
- [ ] White-label capability
- [ ] Partner integration readiness
- [ ] Acquisition/merger flexibility
- [ ] Exit strategy considerations

### Technical Assumptions
- [ ] Cloud-native design
- [ ] Container readiness
- [ ] CI/CD compatibility
- [ ] Infrastructure as code
- [ ] Microservices architecture fit
- [ ] Event-driven capabilities

## Documentation Requirements

For each requirement identified:

1. **Requirement ID**: Unique identifier (E#.F#.C#.R#)
2. **Title**: Clear, concise name
3. **Description**: Detailed explanation
4. **Rationale**: Why this requirement exists
5. **Priority**: Must/Should/Could/Won't (MoSCoW)
6. **Acceptance Criteria**: Measurable success criteria
7. **Test Scenarios**: How to verify
8. **Dependencies**: What it depends on
9. **Risks**: What could go wrong
10. **Assumptions**: What we're taking for granted

## Review Questions

After extraction, ask:
- [ ] Have we captured all stakeholder expectations?
- [ ] Are success criteria measurable?
- [ ] Are edge cases identified?
- [ ] Are failure modes documented?
- [ ] Are non-functional requirements specific?
- [ ] Are dependencies explicit?
- [ ] Are assumptions validated?
- [ ] Are risks assessed?
- [ ] Is the scope clearly bounded?
- [ ] Are interfaces well-defined?