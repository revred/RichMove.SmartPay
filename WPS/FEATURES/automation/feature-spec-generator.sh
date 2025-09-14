#!/bin/bash
#
# Feature Specification Generator
#
# Generates comprehensive feature specifications from the CSV inventory
# Creates detailed documentation following the Epic Elaboration Framework
#
# Usage:
#   ./feature-spec-generator.sh [epic_id] [--all]
#

set -e

# Configuration
CSV_PATH="$(dirname "$0")/../SmartPay_Feature_Inventory.csv"
SPEC_PATH="$(dirname "$0")/../SPECIFICATIONS/generated"
TEMPLATE_PATH="$(dirname "$0")/../SPECIFICATIONS/Epic_Elaboration_Framework.md"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}📝 SmartPay Feature Specification Generator${NC}"
echo ""

# Parse arguments
EPIC_ID=""
GENERATE_ALL=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --all)
            GENERATE_ALL=true
            shift
            ;;
        E*)
            EPIC_ID="$1"
            shift
            ;;
        *)
            echo "Usage: $0 [epic_id] [--all]"
            echo "  epic_id: Generate specs for specific epic (e.g., E2)"
            echo "  --all: Generate specs for all epics"
            exit 1
            ;;
    esac
done

# Ensure output directory exists
mkdir -p "$SPEC_PATH"

# Function to extract epic data from CSV
extract_epic_features() {
    local epic="$1"
    grep "^$epic" "$CSV_PATH" || true
}

# Function to generate capability breakdown
generate_capabilities() {
    local feature_id="$1"
    local feature_name="$2"

    cat << EOF

##### Capability ${feature_id}.C1: Core ${feature_name} Operations

###### Functional Specification
- **Purpose**: [Extracted from business value]
- **Trigger**: [Extracted from invocation]
- **Preconditions**:
  - System is operational
  - Required services available
  - User authorized

- **Process**:
  1. Validate inputs
  2. Check preconditions
  3. Execute core logic
  4. Update system state
  5. Generate response

- **Postconditions**:
  - Operation completed successfully
  - System state updated
  - Audit trail created

- **Outputs**: [Extracted from outcome]

###### Requirements

EOF
}

# Function to generate requirements from CSV data
generate_requirements() {
    local feature_id="$1"
    local requirements="$2"
    local counter=1

    # Parse requirements string and generate detailed specs
    IFS=',' read -ra REQ_ARRAY <<< "$requirements"
    for req in "${REQ_ARRAY[@]}"; do
        cat << EOF
**Requirement ${feature_id}.C1.R${counter}**: ${req}
- **Description**: Detailed implementation of ${req}
- **Rationale**: Business need for this requirement
- **Priority**: Must Have
- **Acceptance Criteria**:
  - [ ] A1: Functional validation passes
  - [ ] A2: Performance targets met
  - [ ] A3: Security controls implemented
  - [ ] A4: Error handling complete

- **Test Scenarios**:
  - T1: Happy path scenario
  - T2: Error condition scenario
  - T3: Edge case scenario
  - T4: Performance test scenario

EOF
        counter=$((counter + 1))
    done
}

# Function to generate NFRs based on risk level
generate_nfrs() {
    local risk_level="$1"

    case "$risk_level" in
        "HIGH")
            cat << EOF
###### Non-Functional Requirements

**Performance**:
- Response Time: p50 <50ms, p95 <100ms, p99 <200ms
- Throughput: 1000 requests/second minimum
- Concurrency: 500 simultaneous operations
- Resource Usage: <500MB memory, <20% CPU

**Reliability**:
- Availability: 99.99% (52.56 minutes downtime/year)
- Error Rate: <0.001%
- Recovery Time: <30 seconds
- Data Durability: 99.999999999%

**Security**:
- Authentication: Multi-factor required
- Authorization: Fine-grained RBAC
- Encryption: AES-256 minimum
- Audit: Complete trail with tamper detection

**Scalability**:
- Horizontal: Linear scaling to 100 nodes
- Vertical: Support up to 128GB RAM
- Data: Petabyte-scale ready
- Geographic: Multi-region support

EOF
            ;;
        "MEDIUM")
            cat << EOF
###### Non-Functional Requirements

**Performance**:
- Response Time: p50 <100ms, p95 <200ms, p99 <500ms
- Throughput: 100 requests/second minimum
- Concurrency: 100 simultaneous operations

**Reliability**:
- Availability: 99.9% (8.76 hours downtime/year)
- Error Rate: <0.01%
- Recovery Time: <5 minutes

**Security**:
- Authentication: Standard OAuth2/JWT
- Authorization: Role-based
- Encryption: TLS 1.2+ required

**Scalability**:
- Horizontal: Scale to 10 nodes
- Vertical: Support up to 16GB RAM

EOF
            ;;
        *)
            cat << EOF
###### Non-Functional Requirements

**Performance**:
- Response Time: p95 <1 second
- Throughput: 10 requests/second minimum

**Reliability**:
- Availability: 99% (3.65 days downtime/year)
- Error Rate: <0.1%

**Security**:
- Basic authentication and authorization
- Standard encryption practices

EOF
            ;;
    esac
}

# Function to generate edge cases based on feature type
generate_edge_cases() {
    local category="$1"

    if [[ "$category" == "User-facing" ]]; then
        cat << EOF
###### Edge Cases

1. **Concurrent Access**:
   - Multiple users accessing same resource
   - Handling: Optimistic locking with retry

2. **Network Interruption**:
   - Connection lost during operation
   - Handling: Resume capability with idempotency

3. **Invalid Input Combinations**:
   - Technically valid but business-invalid inputs
   - Handling: Business rule validation layer

4. **Resource Exhaustion**:
   - System limits reached
   - Handling: Graceful degradation with queuing

5. **Time-based Anomalies**:
   - Timezone changes, daylight saving
   - Handling: UTC internally, local display

EOF
    else
        cat << EOF
###### Edge Cases

1. **Service Unavailability**:
   - Dependent service down
   - Handling: Circuit breaker with fallback

2. **Data Inconsistency**:
   - Conflicting data states
   - Handling: Reconciliation procedures

3. **Performance Degradation**:
   - Slow response from dependencies
   - Handling: Timeout with cached responses

EOF
    fi
}

# Function to generate complete specification for an epic
generate_epic_specification() {
    local epic_id="$1"
    local output_file="$SPEC_PATH/${epic_id}_Specification.md"

    echo -e "${CYAN}Generating specification for Epic $epic_id...${NC}"

    # Extract epic name from first matching line
    epic_line=$(grep "^$epic_id," "$CSV_PATH" | head -1)
    if [ -z "$epic_line" ]; then
        echo -e "${RED}No features found for epic $epic_id${NC}"
        return 1
    fi

    epic_name=$(echo "$epic_line" | cut -d',' -f2)

    # Start generating the specification
    cat > "$output_file" << EOF
# Epic $epic_id: $epic_name - Complete Specification

*Generated: $(date -Iseconds)*

## Executive Summary
This epic encompasses the $epic_name capabilities of the SmartPay platform, providing essential functionality for payment processing and financial operations.

## Business Context

### Problem Statement
[TO BE COMPLETED: Interview stakeholders to capture specific business problem]

### Target Users
- Primary: [TO BE IDENTIFIED]
- Secondary: [TO BE IDENTIFIED]
- Tertiary: [TO BE IDENTIFIED]

### Success Metrics
[TO BE DEFINED: Specific measurable outcomes]

### Business Value
[EXTRACTED FROM CSV - TO BE ELABORATED]

## Technical Context

### System Architecture Impact
[TO BE ANALYZED: Architecture review required]

### Technology Stack
- Core: .NET 9, FastEndpoints
- Data: PostgreSQL, Redis
- Messaging: SignalR, RabbitMQ
- Observability: OpenTelemetry

### Integration Points
[TO BE MAPPED: Integration analysis required]

### Data Model Changes
[TO BE DESIGNED: Data modeling session required]

## Features

EOF

    # Process each feature in the epic
    while IFS=, read -r id name category components status invocation inputs outcome work_package value test_coverage test_ids requirements last_tested test_status regression_risk; do
        # Skip if not matching epic or if header
        if [[ ! "$id" =~ ^$epic_id ]] || [[ "$id" == "ID" ]]; then
            continue
        fi

        # Clean up values
        name=$(echo "$name" | tr -d '"')
        category=$(echo "$category" | tr -d '"')
        components=$(echo "$components" | tr -d '"')
        status=$(echo "$status" | tr -d '"')
        outcome=$(echo "$outcome" | tr -d '"')
        value=$(echo "$value" | tr -d '"')
        requirements=$(echo "$requirements" | tr -d '"')
        regression_risk=$(echo "$regression_risk" | tr -d '"')

        # Generate feature section
        cat >> "$output_file" << EOF

### Feature $id: $name

#### Overview
**Status**: $status
**Category**: $category
**Components**: $components
**Business Value**: $value

#### Current Implementation
- **Invocation**: $invocation
- **Inputs**: $inputs
- **Outcome**: $outcome
- **Test Coverage**: $test_coverage
- **Risk Level**: $regression_risk

#### Capabilities

EOF

        # Generate capabilities
        generate_capabilities "$id" "$name" >> "$output_file"

        # Generate requirements
        if [ -n "$requirements" ]; then
            generate_requirements "$id" "$requirements" >> "$output_file"
        fi

        # Generate NFRs based on risk
        generate_nfrs "$regression_risk" >> "$output_file"

        # Generate edge cases based on category
        generate_edge_cases "$category" >> "$output_file"

        # Add monitoring section
        cat >> "$output_file" << EOF
###### Monitoring & Observability

**Metrics**:
- Operation count by status
- Response time percentiles
- Error rate by type
- Resource utilization

**Logs**:
- All operations (INFO level)
- Errors with context (ERROR level)
- Performance issues (WARN level)

**Alerts**:
- Error rate >1% (P1)
- Response time p99 >SLA (P2)
- Resource usage >80% (P3)

EOF

    done < "$CSV_PATH"

    # Add closing sections
    cat >> "$output_file" << EOF

## Cross-Cutting Concerns

### Security Considerations
[TO BE COMPLETED: Security review required]

### Performance Implications
[TO BE ANALYZED: Performance testing required]

### Data Consistency Requirements
[TO BE DEFINED: Consistency model review]

### Transaction Boundaries
[TO BE MAPPED: Transaction analysis required]

## Migration & Rollout

### Data Migration Requirements
[TO BE PLANNED: Migration strategy session]

### Feature Flags Needed
$(grep "^$epic_id" "$CSV_PATH" | awk -F, '{print "- feature."tolower($1)".enabled"}')

### Rollback Procedures
[TO BE DOCUMENTED: Rollback planning required]

### Training Requirements
[TO BE IDENTIFIED: Training needs assessment]

## Open Questions

[TO BE GATHERED: Stakeholder interviews required]

## Review Status

- [ ] Business stakeholder review
- [ ] Technical architecture review
- [ ] Security review
- [ ] Performance review
- [ ] Compliance review
- [ ] Documentation review

## Next Steps

1. Schedule stakeholder interviews to complete business context
2. Conduct technical deep-dive sessions for each feature
3. Perform security threat modeling
4. Define detailed test scenarios
5. Create implementation plan

---

*This specification requires completion through stakeholder engagement and technical analysis sessions.*
EOF

    echo -e "${GREEN}✅ Generated specification: $output_file${NC}"
}

# Main execution
if [ "$GENERATE_ALL" = true ]; then
    echo -e "${CYAN}Generating specifications for all epics...${NC}"
    echo ""

    # Extract unique epic IDs
    epic_ids=$(cut -d',' -f1 "$CSV_PATH" | grep '^E[0-9]' | cut -d'.' -f1 | sort -u)

    for epic in $epic_ids; do
        generate_epic_specification "$epic"
    done

    echo ""
    echo -e "${GREEN}✅ Generated specifications for all epics${NC}"

elif [ -n "$EPIC_ID" ]; then
    generate_epic_specification "$EPIC_ID"

else
    echo "Usage: $0 [epic_id] [--all]"
    echo ""
    echo "Examples:"
    echo "  $0 E2        # Generate spec for Epic E2"
    echo "  $0 --all     # Generate specs for all epics"
    exit 1
fi

# Summary
echo ""
echo -e "${CYAN}📊 Generation Summary:${NC}"
echo -e "${GRAY}  Output directory: $SPEC_PATH${NC}"
echo -e "${GRAY}  Specifications generated: $(ls -1 "$SPEC_PATH"/*.md 2>/dev/null | wc -l)${NC}"
echo ""
echo -e "${YELLOW}⚠️  Note: Generated specifications require completion through:${NC}"
echo -e "${GRAY}  1. Stakeholder interviews${NC}"
echo -e "${GRAY}  2. Technical analysis sessions${NC}"
echo -e "${GRAY}  3. Security reviews${NC}"
echo -e "${GRAY}  4. Performance analysis${NC}"
echo -e "${GRAY}  5. Compliance validation${NC}"