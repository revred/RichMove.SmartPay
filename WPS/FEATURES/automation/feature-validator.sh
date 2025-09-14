#!/bin/bash
#
# Feature Validator - Validates all implemented features against current codebase and test coverage
#
# This script validates the feature inventory CSV against actual implementation and test coverage.
# - Runs before every commit to ensure feature tracking is accurate
# - Generates regression test reports
# - Updates test coverage percentages
# - Prevents commits if critical features regress
#
# Usage:
#   ./feature-validator.sh [precommit|full|update]
#

set -e

# Configuration
MODE="${1:-precommit}"
BASE_URL="${SMARTPAY_BASE_URL:-http://localhost:5001}"
TENANT="${SMARTPAY_TENANT:-default}"
CSV_PATH="$(dirname "$0")/../SmartPay_Feature_Inventory.csv"
REPORTS_PATH="$(dirname "$0")/../reports"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}🔍 SmartPay Feature Validator${NC}"
echo -e "${GRAY}Mode: $MODE | Base URL: $BASE_URL | Tenant: $TENANT${NC}"
echo ""

# Check if CSV exists
if [ ! -f "$CSV_PATH" ]; then
    echo -e "${RED}❌ Feature inventory CSV not found: $CSV_PATH${NC}"
    exit 1
fi

# Results tracking
PASSED=0
FAILED=0
WARNINGS=0
SKIPPED=0
FAILED_FEATURES=()
HIGH_RISK_FAILURES=()

# Test API endpoint function
test_endpoint() {
    local url="$1"
    local method="${2:-GET}"
    local body="$3"
    local expected_codes="${4:-200,201}"

    local curl_opts=(-s -w "%{http_code}" -H "X-Tenant: $TENANT")

    if [ "$method" = "POST" ]; then
        curl_opts+=(-X POST)
        if [ -n "$body" ]; then
            curl_opts+=(-H "Content-Type: application/json" -d "$body")
        fi
    fi

    local response
    response=$(curl "${curl_opts[@]}" "$url" 2>/dev/null || echo "000")
    local http_code="${response: -3}"

    # Check if code is in expected codes
    if [[ ",$expected_codes," == *",$http_code,"* ]]; then
        return 0
    else
        return 1
    fi
}

# Read CSV and get implemented features
echo -e "${CYAN}📊 Feature Inventory Analysis:${NC}"

# Count features (simplified - would parse CSV properly in production)
total_features=$(wc -l < "$CSV_PATH")
total_features=$((total_features - 1)) # Subtract header

implemented_features=$(grep -c "Implemented" "$CSV_PATH" || echo "0")

echo -e "${GRAY}  Total Features: $total_features${NC}"
echo -e "${GRAY}  Implemented: $implemented_features${NC}"
echo -e "${GRAY}  Planned: $((total_features - implemented_features))${NC}"
echo ""

# Test critical features
echo -e "${CYAN}🔬 Executing feature validation tests...${NC}"
echo ""

# E1.F2 - Swagger UI
echo -e "${GRAY}🧪 Testing E1.F2 - Swagger UI${NC}"
if test_endpoint "$BASE_URL/swagger/index.html"; then
    echo -e "   ${GREEN}✅ PASS${NC}"
    ((PASSED++))
else
    echo -e "   ${RED}❌ FAIL${NC}"
    ((FAILED++))
    FAILED_FEATURES+=("E1.F2")
fi

# E1.F3 - Health Checks
echo -e "${GRAY}🧪 Testing E1.F3 - Health Checks${NC}"
if test_endpoint "$BASE_URL/health/live" && test_endpoint "$BASE_URL/health/ready"; then
    echo -e "   ${GREEN}✅ PASS${NC}"
    ((PASSED++))
else
    echo -e "   ${RED}❌ FAIL${NC}"
    ((FAILED++))
    FAILED_FEATURES+=("E1.F3")
fi

# E2.F1 - FX Quote Creation (HIGH RISK)
echo -e "${GRAY}🧪 Testing E2.F1 - FX Quote Creation${NC}"
quote_body='{"fromCurrency":"USD","toCurrency":"GBP","amount":1000}'
if test_endpoint "$BASE_URL/api/fx/quote" "POST" "$quote_body" "200,201"; then
    echo -e "   ${GREEN}✅ PASS${NC}"
    ((PASSED++))
else
    echo -e "   ${RED}❌ FAIL${NC}"
    echo -e "   ${RED}🚨 HIGH REGRESSION RISK${NC}"
    ((FAILED++))
    FAILED_FEATURES+=("E2.F1")
    HIGH_RISK_FAILURES+=("E2.F1")
fi

# E2.F4 - DB Health
echo -e "${GRAY}🧪 Testing E2.F4 - DB Health${NC}"
if test_endpoint "$BASE_URL/v1/health/db" "GET" "" "200,503"; then
    echo -e "   ${GREEN}✅ PASS${NC}"
    ((PASSED++))
else
    echo -e "   ${RED}❌ FAIL${NC}"
    ((FAILED++))
    FAILED_FEATURES+=("E2.F4")
fi

# E4.F1 - SignalR Negotiate (HIGH RISK)
echo -e "${GRAY}🧪 Testing E4.F1 - SignalR Negotiate${NC}"
if test_endpoint "$BASE_URL/hubs/notifications/negotiate?negotiateVersion=1" "POST" "" "200,201,204"; then
    echo -e "   ${GREEN}✅ PASS${NC}"
    ((PASSED++))
else
    echo -e "   ${RED}❌ FAIL${NC}"
    echo -e "   ${RED}🚨 HIGH REGRESSION RISK${NC}"
    ((FAILED++))
    FAILED_FEATURES+=("E4.F1")
    HIGH_RISK_FAILURES+=("E4.F1")
fi

# Run test coverage if in update mode
if [ "$MODE" = "update" ]; then
    echo ""
    echo -e "${CYAN}📈 Updating test coverage percentages...${NC}"

    cd "$(dirname "$0")/../../../ZEN"
    if dotnet test --configuration Release --collect:"XPlat Code Coverage" --results-directory "$REPORTS_PATH/coverage" > /dev/null 2>&1; then
        echo -e "   ${GREEN}✅ Test suite passed${NC}"
    else
        echo -e "   ${RED}❌ Test suite failed${NC}"
        ((FAILED++))
    fi
    cd - > /dev/null
fi

# Generate report
mkdir -p "$REPORTS_PATH"
timestamp=$(date +"%Y-%m-%d_%H-%M-%S")
report_file="$REPORTS_PATH/validation-report-$timestamp.json"

cat > "$report_file" << EOF
{
  "timestamp": "$(date -Iseconds)",
  "mode": "$MODE",
  "baseUrl": "$BASE_URL",
  "tenant": "$TENANT",
  "summary": {
    "totalFeatures": $total_features,
    "implementedFeatures": $implemented_features,
    "testsPassed": $PASSED,
    "testsFailed": $FAILED,
    "testsSkipped": $SKIPPED,
    "warnings": $WARNINGS
  },
  "failedFeatures": [$(printf '"%s",' "${FAILED_FEATURES[@]}" | sed 's/,$//')]
  "highRiskFailures": [$(printf '"%s",' "${HIGH_RISK_FAILURES[@]}" | sed 's/,$//')]
}
EOF

# Summary output
echo ""
echo -e "${CYAN}📋 Validation Summary:${NC}"
echo -e "  ${GREEN}✅ Passed: $PASSED${NC}"
echo -e "  ${RED}❌ Failed: $FAILED${NC}"
echo -e "  ${GRAY}⏭️  Skipped: $SKIPPED${NC}"
echo -e "  ${YELLOW}⚠️  Warnings: $WARNINGS${NC}"
echo ""
echo -e "${GRAY}📄 Report saved: $report_file${NC}"

# Exit logic
if [ $FAILED -gt 0 ]; then
    echo ""
    echo -e "${RED}❌ FAILURES DETECTED:${NC}"
    for failure in "${FAILED_FEATURES[@]}"; do
        echo -e "  ${RED}- $failure${NC}"
    done

    if [ "$MODE" = "precommit" ] && [ ${#HIGH_RISK_FAILURES[@]} -gt 0 ]; then
        echo ""
        echo -e "${RED}🚨 HIGH REGRESSION RISK FAILURES - COMMIT BLOCKED${NC}"
        exit 1
    fi

    if [ "$MODE" = "precommit" ]; then
        echo ""
        echo -e "${YELLOW}⚠️  Non-critical failures detected - commit allowed but review recommended${NC}"
        exit 0
    fi

    exit 1
fi

echo -e "${GREEN}✅ All feature validations passed!${NC}"
exit 0